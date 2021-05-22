using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace X3Util
{
    public partial class MainForm : Form
    {
        const string ProjectUrl = "https://github.com/Ernegien/X3Util";
        const int EepromBankSize = 256;
        const int EepromSize = EepromBankSize * 4;
        static byte[] ControlSeed = new byte[] { 0x17, 0xA8, 0xD0, 0x84, 0xA3, 0x86, 0x7C, 0x40, 0x6E, 0x6C, 0x2A, 0xAD, 0xFB, 0x7C, 0x86, 0x71 };
        static byte[] EepromSeed = new byte[] { 0x70, 0x6F, 0x2E, 0xB3, 0x66, 0x40, 0x7B, 0x32, 0xDF, 0x23, 0xA7, 0x31, 0x31, 0x3F, 0xB4, 0x21 };

        public MainForm()
        {
            InitializeComponent();
        }

        private void btnCrypt_Click(object sender, EventArgs e)
        {
            // EEPROM Layout
            //Bank 1 - X3 Config LCD Message (RC4 encrypted)
            //Bank 2 - Xbox EEPROM Backup (plaintext)
            //Bank 3 - Unused 0xFF's (plaintext)
            //Bank 4 - X3 Config (RC4 encrypted)

            try
            {
                // prompt for file
                string file = null;
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Open X3 EEPROM file...";
                    ofd.FileName = "EEPROM.bin";
                    ofd.Filter = "EEPROM Backup (*.bin)|*.bin|All Files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        file = ofd.FileName;
                    }
                    else throw new Exception("Operation cancelled!");
                }

                using FileStream fs = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                using BinaryReader br = new BinaryReader(fs);
                using BinaryWriter bw = new BinaryWriter(fs);

                if (fs.Length != EepromSize)
                    throw new ArgumentOutOfRangeException("Supplied file must be 1KB in size!");

                // check for valid X3 magic
                ushort magic = br.ReadUInt16();
                if (magic != 0x3358 && magic != 0x7208)
                    throw new InvalidDataException("Not a valid X3 EEPROM image!");

                // read the entire eeprom
                fs.Position = 0;
                byte[] eeprom = br.ReadBytes((int)fs.Length);

                // crypt the X3-specific portions
                RC4 rc4 = new RC4();
                rc4.Init(EepromSeed);
                rc4.Crypt(eeprom, 0, EepromBankSize);      // x3 lcd message
                rc4.Init(EepromSeed);
                rc4.Crypt(eeprom, EepromBankSize * 3, EepromBankSize);    // x3 config

                // overwrite the entire eeprom
                fs.Position = 0;
                bw.Write(eeprom);
            }
            catch (Exception ex)
            {
                DisplayMessage("Failed to en/de-crypt EEPROM!\n\n" + ex.ToString(), true);
            }
        }

        private void btnExtract_Click(object sender, EventArgs e)
        {
            try
            {
                // get remainder.bin input file path
                string remainderPath = null;
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Open X3 BIOS remainder.img file...";
                    ofd.FileName = "remainder.img";
                    ofd.Filter = "BIOS Remainder (*.img)|*.img|All Files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        remainderPath = ofd.FileName;
                    }
                    else throw new Exception("Operation cancelled!");
                }

                using (FileStream fs = File.OpenRead(remainderPath))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    // read image info
                    fs.Position = 0x5200 - 0x10;
                    uint x3Version = br.ReadUInt32();
                    int compressedXbeSize = br.ReadInt32();
                    int xbeSize = br.ReadInt32();
                    uint leet = br.ReadUInt32();
                    if (x3Version > 4000 || leet != 0x7433336C)  // sanity checks to better ensure this is a valid X3 remainder.bin
                        throw new Exception("Invalid X3 remainder.img!");

                    // RC4 decrypt
                    byte[] encryptedData = br.ReadBytes(compressedXbeSize);
                    RC4 rc4 = new RC4();
                    rc4.Init(ControlSeed);
                    rc4.Crypt(encryptedData, 0, encryptedData.Length);

                    // get x3control.xbe output file path
                    string x3ControlPath = null;
                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.Title = "Save extracted x3control.xbe file...";
                        sfd.FileName = "x3control.xbe";
                        sfd.Filter = "Xbox Executable (*.xbe)|*.xbe|All Files (*.*)|*.*";
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            x3ControlPath = sfd.FileName;
                        }
                        else throw new Exception("Operation cancelled!");
                    }

                    // zlib decompress
                    using (FileStream output = new FileStream(x3ControlPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (MemoryStream input = new MemoryStream(encryptedData))
                    using (InflaterInputStream iis = new InflaterInputStream(input))
                    {
                        iis.CopyTo(output);
                        if (output.Length != xbeSize)
                            throw new Exception("Extracted output doesn't match advertised xbe size!");
                    }
                }

                DisplayMessage("Done");
            }
            catch (Exception ex)
            {
                DisplayMessage("Failed to extract control xbe!\n\n" + ex.ToString(), true);
            }
        }

        private void btnInject_Click(object sender, EventArgs e)
        {
            try
            {
                // get x3control.xbe input file path
                string x3ControlPath = null;
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Open x3control.xbe file...";
                    ofd.FileName = "x3control.xbe";
                    ofd.Filter = "Xbox Executable (*.xbe)|*.xbe|All Files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        x3ControlPath = ofd.FileName;
                    }
                    else throw new Exception("Operation cancelled!");
                }

                // get remainder.bin input/output file path
                string remainderPath = null;
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Open X3 BIOS remainder.img file...";
                    ofd.FileName = "remainder.img";
                    ofd.Filter = "BIOS Remainder (*.img)|*.img|All Files (*.*)|*.*";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        remainderPath = ofd.FileName;
                    }
                    else throw new Exception("Operation cancelled!");
                }

                using (MemoryStream output = new MemoryStream())
                using (FileStream xbeInput = File.OpenRead(x3ControlPath))
                using (DeflaterOutputStream dos = new DeflaterOutputStream(output))
                using (FileStream remainderOutput = new FileStream(remainderPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                using (BinaryWriter bw = new BinaryWriter(remainderOutput))
                {
                    // zlib compress
                    xbeInput.CopyTo(dos);
                    dos.Finish();

                    // RC4 encrypt
                    byte[] compressedData = output.ToArray();
                    RC4 rc4 = new RC4();
                    rc4.Init(ControlSeed);
                    rc4.Crypt(compressedData, 0, compressedData.Length);

                    // save new size info
                    remainderOutput.Position = 0x5200 - 0xC;
                    bw.Write((uint)compressedData.Length);
                    bw.Write((uint)xbeInput.Length);
                    remainderOutput.Position += 4;  // skip the l33t tag

                    // write compressed encrypted xbe image into the remainder
                    remainderOutput.Write(compressedData, 0, compressedData.Length);

                    // TODO: zero out remaining unused portion of remainder

                    DisplayMessage("Done");
                }
            }
            catch (Exception ex)
            {
                DisplayMessage("Failed to inject control xbe!\n\n" + ex.ToString(), true);
            }
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", ProjectUrl);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", ProjectUrl);
                }
                else
                {
                    Process.Start(ProjectUrl);
                }
            }
            catch
            {
                DisplayMessage(ProjectUrl);
            }
        }

        private static void DisplayMessage(string msg, bool error = false)
        {
            MessageBox.Show(msg, error ? "Error" : "Info", MessageBoxButtons.OK,
                error ? MessageBoxIcon.Error : MessageBoxIcon.Information);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ShadowrunLauncher.Logic
{
    internal class GenerateKeyLogic
    {
        private InstallLogic _installLogic;

        public GenerateKeyLogic(InstallLogic installLogic)
        {
            _installLogic = installLogic;
        }

        internal string GenerateKeyButtonClickLogic()
        {
            // Fetch and print PCID from the registry at the beginning of the GenerateKey method
            string pcidDecimal = RegistryLogic.GetPcidFromRegistry();
            string pcid2 = "";
            if (!string.IsNullOrEmpty(pcidDecimal))
            {
                pcid2 = HelperMethods.DecimalToHexFormat(long.Parse(pcidDecimal));
                Console.WriteLine($"PCID from registry (decimal): {pcidDecimal}");
                Console.WriteLine($"PCID from registry (hex format): {pcid2}");
            }
            else
            {
                Console.WriteLine("PCID not found in the registry.");
            }

            // Check for the existence of the PCID in the Windows Registry
            if (!RegistryLogic.CheckPcidInRegistry())
            {
                var response = MessageBox.Show("You have not created a PCID yet. To create one, you must launch the game and then exit it. Would you like to launch the game?", "Notification", MessageBoxButton.YesNo);

                if (response == MessageBoxResult.Yes) // If the user clicked "Yes"
                {
                    _installLogic.PlayButtonClickLogic();
                }
                else
                {
                    // TODO
                }
                return "";
            }

            KeyData keyData = GetRandomKeyData();
            string pcid = keyData.Pcid;
            string key = keyData.Key;
            Console.WriteLine($"PCID: {pcid}");

            // Copy the key to the clipboard
            HelperMethods.CopyToClipboard(key);

            // Write registry modifications to a .reg file
            string registryModificationContent;
            if (RegistryLogic.SrPcidChangeExists())
            {
                Console.WriteLine("IT IS HERE");
                registryModificationContent = $"Windows Registry Editor Version 5.00\n\n[HKEY_CURRENT_USER\\Software\\Classes\\SOFTWARE\\Microsoft\\XLive]\n\"PCID\"=hex(b):{pcid}";
            }
            else
            {
                Console.WriteLine("IT IS NOT HERE");
                registryModificationContent = $"Windows Registry Editor Version 5.00\n\n[HKEY_CURRENT_USER\\Software\\Classes\\SOFTWARE\\Microsoft\\XLive]\n\"PCID\"=hex(b):{pcid}\n\"SRPCIDBACKUP\"=hex(b):{pcid2}";
            }

            // Write registry modification content to .reg file
            File.WriteAllText("reg.reg", registryModificationContent);

            // Import registry modifications from the .reg file
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "reg",
                Arguments = "import reg.reg",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true // Prevents the command prompt window from appearing
            };

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
            }

            // Delete Token.bin files
            string[] pathsToCheck = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "XLive", "Titles", "4d5307d6", "Token.bin"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "XLive", "Titles", "4d5307d6", "Token.bin")
            };

            // Loop through the paths and delete the file if it exists
            foreach (string filePath in pathsToCheck)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            Console.WriteLine("Updating UI with generated key...");
            return key;
            //OpenKeyWindow(key);
        }
        public void DeleteToken()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path = System.IO.Path.Combine(path, @"Microsoft\Xlive\Titles\4d5307d6");
            string TokenFile = path + "\\Token.bin";
            if (File.Exists(TokenFile))
            {
                File.Delete(TokenFile);
            }
        }

        private List<KeyData> data = new List<KeyData>
        {
            new KeyData { Pcid = "90,c8,9a,8a,f6,57,a9,f7", Key = "FXRHK-T8PDY-FHBCH-G6YJG-XF8PJ" },
            new KeyData { Pcid = "23,b3,bd,92,12,6b,c5,fc", Key = "WBV4B-MFGR4-Y9XY7-MKRPM-HHJ96" },
            new KeyData { Pcid = "f3,42,a3,bb,c2,43,96,3c", Key = "M2RHB-TDC2R-MTHXH-GP662-XG33W" },
            new KeyData { Pcid = "ec,9f,31,39,87,50,96,62", Key = "WMBGX-HC2WR-JC92D-KVK2B-Q8YB3" },
            new KeyData { Pcid = "92,1e,37,df,f4,52,dd,01", Key = "W6Y9P-MKP4C-FHT83-GQMMC-FYQQQ" },
            new KeyData { Pcid = "53,48,b4,94,11,01,d5,98", Key = "JX3JC-CC2HX-TRWCY-49BKF-2CKYY" },
            new KeyData { Pcid = "eb,5f,76,a9,5d,87,d9,8d", Key = "XH3CX-7D382-4TWG7-D9YT9-FFJMJ" },
            new KeyData { Pcid = "ec,78,57,e2,44,57,f2,91", Key = "V7HM9-K3YBQ-K3XVF-4K6JF-RXXHG" },
            new KeyData { Pcid = "2b,2c,e1,af,16,ea,97,42", Key = "DCKXY-JG4DH-JRDYB-KMT97-GPKPG" },
            new KeyData { Pcid = "d8,bf,b3,fd,49,11,9f,a7", Key = "QGTD9-VM883-83FPP-KYKD2-FK3JD" },
            new KeyData { Pcid = "39,49,34,34,30,c0,7b,97", Key = "MVKG6-8BPRK-93FPR-GTH9Y-GQJWT" },
            new KeyData { Pcid = "94,52,8c,e7,c8,e0,bc,80", Key = "P72WF-GXDQM-8YTP4-7TYYB-72YGT" },
            new KeyData { Pcid = "1c,53,6d,92,2d,ef,26,76", Key = "JY6GC-GD69H-G4TC2-BF9MJ-FW9YJ" },
            new KeyData { Pcid = "91,26,c1,de,83,b8,f8,f5", Key = "Q38PK-B9WCR-8D8WP-C8Y28-9DW73" },
            new KeyData { Pcid = "f7,b0,2f,e2,18,ed,05,e8", Key = "GXTHG-JCQMJ-WVBCP-MDVPV-JBX43" },
            new KeyData { Pcid = "7e,1a,c9,0c,6d,aa,4f,b1", Key = "RHQV3-7G3FM-9T4CD-F9H8B-FT66Q" },
            new KeyData { Pcid = "ab,49,55,39,77,46,1d,90", Key = "CMBMJ-CG3PC-R2HY8-6RYGG-CRWTY" },
            new KeyData { Pcid = "d9,bd,df,b7,aa,f9,b3,fd", Key = "CPTJV-PYQRR-VY79Y-7PMM6-DWBF3" },
            new KeyData { Pcid = "40,f1,a1,5e,11,39,83,f2", Key = "22TBG-D3PF4-YPMDJ-MMJ8Q-9Y68G" },
            new KeyData { Pcid = "ad,28,cc,13,2c,e2,b3,61", Key = "CTJG4-V3MQY-3K272-6MHCV-R4GG6" },
            new KeyData { Pcid = "e3,c3,e6,3d,94,87,26,2e", Key = "V3K6V-QTKQD-RDWCJ-X3WM2-G8XP8" },
            new KeyData { Pcid = "f8,37,19,46,ce,45,44,45", Key = "DPQ7P-646DC-DM63Q-X4YD3-MPBMB" },
            new KeyData { Pcid = "24,59,1c,b5,37,2e,65,fe", Key = "CHYXY-QYRXP-WR22C-8B47X-DGF93" },
            new KeyData { Pcid = "71,40,3a,3b,ad,26,98,5d", Key = "RRQ6J-B2G7T-GMW8M-Q7QYX-3VJVQ" },
            new KeyData { Pcid = "d0,9e,87,b9,41,95,85,f5", Key = "FYGQP-F7GQP-X6CX6-BFYVK-WQBBG" },
            new KeyData { Pcid = "86,0c,7e,9b,a5,08,5a,31", Key = "HJKY7-6TQD6-6FPXT-DG9J3-K7YQD" },
            new KeyData { Pcid = "df,fa,68,3e,ed,ab,07,3b", Key = "VY43Y-JYC9Q-84T4P-M22G8-WVBR6" },
            new KeyData { Pcid = "b6,37,7a,64,a9,f7,36,a3", Key = "BYMGW-K33C2-WDDDD-VQ98P-DJC4M" },
            new KeyData { Pcid = "8c,b1,87,93,cd,fa,91,85", Key = "G8FFP-FRBT6-DCKT9-HRMMX-XCMBJ" },
            new KeyData { Pcid = "84,a4,aa,7e,36,8a,45,b3", Key = "DW9FC-B2DFG-TQB9Y-P3YKC-V8P7Y" },
            new KeyData { Pcid = "f6,01,85,6f,40,1e,26,61", Key = "VDQBM-TYB29-QTRG6-WY7VB-YRD7J" },
            new KeyData { Pcid = "8a,c8,1d,58,52,bb,e6,88", Key = "W8D7H-F2RBK-PRHCG-PRTQW-CHPDB" }
            // Add the rest of the PC IDs and keys here
        };

        private KeyData GetRandomKeyData()
        {
            Random random = new Random();
            int index = random.Next(data.Count); // Generate a random index within the range of the data list
            return data[index]; // Return the KeyData object at the random index
        }
    }
    public class KeyData
    {
        public string Pcid { get; set; }
        public string Key { get; set; }
    }
}

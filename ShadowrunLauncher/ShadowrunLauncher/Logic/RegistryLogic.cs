using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadowrunLauncher.Logic
{
    internal class RegistryLogic
    {
        internal static bool CheckPcidInRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SOFTWARE\Microsoft\XLive", false))
                {
                    if (key != null)
                    {
                        object pcidValue = key.GetValue("PCID");
                        return pcidValue != null;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error accessing registry: {e}");
            }
            return false;
        }
        internal static string GetPcidFromRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SOFTWARE\Microsoft\XLive", false))
                {
                    if (key != null)
                    {
                        object pcidValue = key.GetValue("PCID");
                        return pcidValue?.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error accessing registry: {e}");
            }
            return null;
        }
        internal static void SetPcidInRegistry(string decimalValue)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SOFTWARE\Microsoft\XLive", true))
                {
                    if (key != null)
                    {
                        key.SetValue("PCID", decimalValue, RegistryValueKind.QWord);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error setting PCID in registry: {e}");
            }
        }
        internal static bool SrPcidChangeExists()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SOFTWARE\Microsoft\XLive", false))
                {
                    if (key != null)
                    {
                        object srPcidBackup = key.GetValue("SRPCIDBACKUP");
                        return srPcidBackup != null;
                    }
                }
            }
            catch (Exception)
            {
                // If an exception occurs, it means the key does not exist
            }
            return false;
        }
        internal static void DeleteSrPcidBackupFromRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SOFTWARE\Microsoft\XLive", true))
                {
                    if (key != null)
                    {
                        key.DeleteValue("SRPCIDBACKUP");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error deleting SRPCIDBACKUP from registry: {e}");
            }
        }
        internal static string GetSrPcidBackupFromRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SOFTWARE\Microsoft\XLive"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("SRPCIDBACKUP");
                        if (value != null)
                        {
                            return value.ToString();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Handle exceptions, or you can log them if needed
            }
            return null;
        }
    }
}

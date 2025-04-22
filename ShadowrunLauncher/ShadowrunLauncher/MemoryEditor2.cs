using System.Diagnostics;
using System.Runtime.InteropServices;



public class MemoryEditor2 : IDisposable
{

    public Process CurrentProcess { get; set; }
    public MemoryEditor2(string processname)
    {
        CurrentProcess = GetProcessByName(processname);
    }
    public enum MiniDumpType : uint
    {
        WithFullMemory = 2
    }
    [Flags]
    public enum ProcessAccessFlags : uint
    {
        All = 0x001F0FFF,
        Terminate = 0x00000001,
        CreateThread = 0x00000002,
        VMOperation = 0x00000008,
        VMRead = 0x00000010,
        VMWrite = 0x00000020,
        DupHandle = 0x00000040,
        SetInformation = 0x00000200,
        QueryInformation = 0x00000400,
        Synchronize = 0x00100000
    }
    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("Dbghelp.dll")]
    public static extern bool MiniDumpWriteDump(IntPtr hProcess, uint ProcessId, SafeHandle hFile, MiniDumpType DumpType, IntPtr ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);
    public const int PROCESS_ALL_ACCESS = 0x1F0FFF; // Adjust this as needed
    public const int PAGE_EXECUTE_READWRITE = 0x40; // Adjust this as needed
    const int MAX_CHUNK_SIZE = 4096; // Adjust the chunk size as needed
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out uint lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    public static bool WriteValue(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out uint lpNumberOfBytesWritten)
    {
        uint previousAccessProtect = 0;
        VirtualProtectEx(hProcess, lpBaseAddress, nSize, PAGE_EXECUTE_READWRITE, out previousAccessProtect);
        bool result = WriteProcessMemory(hProcess, lpBaseAddress, lpBuffer, nSize, out lpNumberOfBytesWritten);
        VirtualProtectEx(hProcess, lpBaseAddress, nSize, previousAccessProtect, out previousAccessProtect);
        return result;
    }

    public byte[] ReadMemory(int processId, IntPtr address, int length)
    {
        IntPtr processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, processId);

        if (processHandle != IntPtr.Zero)
        {
            byte[] buffer = new byte[MAX_CHUNK_SIZE];
            int totalBytesRead = 0;
            int bytesRead;

            using (var memoryStream = new System.IO.MemoryStream())
            {
                while (totalBytesRead < length)
                {
                    int chunkSize = Math.Min(MAX_CHUNK_SIZE, length - totalBytesRead);
                    if (ReadProcessMemory(processHandle, address + totalBytesRead, buffer, (uint)chunkSize, out bytesRead))
                    {
                        memoryStream.Write(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                    }
                    else
                    {
                        // Handle the read failure
                        break;
                    }
                }

                CloseHandle(processHandle);
                return memoryStream.ToArray();
            }
        }

        return null;
    }


    /*public bool WriteMemory(int processId, IntPtr address, byte[] data)
    {
        IntPtr processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, processId);

        if (processHandle != IntPtr.Zero)
        {
            int bytesWritten;

            if (WriteProcessMemory(processHandle, address, data, (uint)data.Length, out bytesWritten))
            {
                CloseHandle(processHandle);
                return true;
            }
            else
            {
                // Handle the write failure
            }
        }

        return false;
    }*/
    private static Process GetProcessByName(string processName)
    {
        foreach (var process in Process.GetProcesses())
        {
            if (process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
            {
                return process;
            }
        }
        return null;
    }
    public float GetFloatFromMemory(int processId, IntPtr address)
    {
        // Read 4 bytes from memory, as a float is 4 bytes
        byte[] memoryBytes = ReadMemory(processId, address, 4);

        if (memoryBytes != null && memoryBytes.Length == 4)
        {
            // Convert the bytes to a float
            return BitConverter.ToSingle(memoryBytes, 0);
        }

        throw new InvalidOperationException("Unable to read float value from memory.");
    }
    /*public bool SetFloatInMemory(int processId, IntPtr address, float value)
    {
        // Convert the float to a byte array
        byte[] floatBytes = BitConverter.GetBytes(value);

        // Write the byte array to the specified memory address
        return WriteMemory(processId, address, floatBytes);
    }*/
    // Method to set a float value in the process's memory
    public float SetFloatValue(int processId, IntPtr baseAddress, float value)
    {
        IntPtr processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, processId);

        if (processHandle != IntPtr.Zero)
        {

            try
            {
                // Convert the float value to byte array
                byte[] buffer = BitConverter.GetBytes(value);

                // Write the float value to the final memory address
                uint bytesWritten;

                if (WriteValue(processHandle, baseAddress, buffer, (uint)buffer.Length, out bytesWritten))
                {
                    Console.WriteLine($"Successfully changed to {value} at memory address {baseAddress.ToString("X")}");
                }
                else
                {
                    Console.WriteLine($"Failed to write memory at {baseAddress.ToString("X")}");
                }
            }
            finally
            {
                
                // Close the process handle
                CloseHandle(processHandle);
            }
        }
        return GetFloatFromMemory(processId, baseAddress);
    }

    public void Dispose()
    {

    }
    internal int GetProcessID()
    {
       return CurrentProcess.Id;

    }
    internal nint CheatTable(nint relativeAddress, List<int> offsets = null)
    {
        // Get the base address from the process module
        nint baseAddress = CurrentProcess.MainModule.BaseAddress;

        // Compute the initial address (base + relative)
        nint currentAddress = baseAddress + relativeAddress;
        Console.WriteLine($"Base Address: {baseAddress:X}, Relative Address: {relativeAddress:X}, Initial Address: {currentAddress:X}");

        // If no offsets are provided, return the computed address
        if (offsets == null || offsets.Count == 0)
        {
            return currentAddress;
        }

        offsets.Reverse();

        // Resolve the pointer chain
        foreach (var offset in offsets)
        {
            // Read the value at the current address
            byte[] memoryBytes = ReadMemory(GetProcessID(), currentAddress, 4);
            if (memoryBytes == null || memoryBytes.Length != 4)
            {
                throw new Exception($"Failed to read memory at address {currentAddress:X}");
            }

            // Dereference the pointer (convert the bytes to an integer)
            int pointerValue = BitConverter.ToInt32(memoryBytes, 0);
            Console.WriteLine($"Pointer Value at Address {currentAddress:X}: {pointerValue:X}");

            // Compute the next address by adding the offset
            currentAddress = (nint)(pointerValue + offset);
            Console.WriteLine($"New Address after Offset {offset:X}: {currentAddress:X}");
        }

        // Final resolved address
        Console.WriteLine($"Final Resolved Address: {currentAddress:X}");
        return currentAddress;
    }
    //internal nint CheatTable(nint relativeAddress, List<int> offsets = null)
    //{
    //    // Get the base address from the process module
    //    nint baseAddress = CurrentProcess.MainModule.BaseAddress;
    //    if (offsets != null && offsets.Count > 0)
    //    {
    //        offsets.Reverse();

    //        // Get the relative address part and add to the base address
    //        nint address = baseAddress + relativeAddress;

    //        // Read 4 bytes from memory, as a float is 4 bytes
    //        byte[] memoryBytes = ReadMemory(GetProcessID(), address, 4);

    //        // Apply the offsets one by one
    //        foreach (var offset in offsets)
    //        {
    //            // Convert reversed byte array to nint
    //            var y = BitConverter.ToInt32(memoryBytes, 0);
    //            var x = y + offset;

    //            // Read memory again using the new address (x)
    //            var nintx = (nint)x;
    //            memoryBytes = ReadMemory(GetProcessID(), nintx, 4);

    //            Console.WriteLine($"memoryBytes: {BitConverter.ToString(memoryBytes)}");
    //        }

    //        // Final memory address after applying offsets
    //        var finalAddress = BitConverter.ToInt32(memoryBytes, 0);
    //        Console.WriteLine($"Final Address: {finalAddress}");

    //        return (nint)finalAddress;
    //    }
    //    else
    //    {
    //        return baseAddress + relativeAddress;
    //    }
    //}

}



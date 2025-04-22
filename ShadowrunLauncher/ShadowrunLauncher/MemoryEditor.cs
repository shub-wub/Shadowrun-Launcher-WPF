using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

/// <summary>
/// Provides functionality for editing the memory of a target process.
/// </summary>
/// <remarks>
/// This class allows for reading, writing, freezing, and manipulating memory in a specified process.
/// It also includes utility functions for resolving pointer chains, scanning memory, and interacting with processes.
/// </remarks>
public class MemoryEditor : IDisposable
{

    /// <summary>
    /// The maximum size of a chunk to read from memory at one time.
    /// </summary>
    private const int MAX_CHUNK_SIZE = 4096;

    /// <summary>
    /// Memory protection flag for read, write, and execute permissions.
    /// </summary>
    public const int PAGE_EXECUTE_READWRITE = 0x40;

    /// <summary>
    /// Access flag for granting full control over a process.
    /// </summary>
    public const int PROCESS_ALL_ACCESS = 0x1F0FFF;

    /// <summary>
    /// Indicates whether the object has already been disposed to prevent redundant calls to Dispose.
    /// </summary>
    private bool disposed = false;

    /// <summary>
    /// Stores threads responsible for freezing values at specific memory addresses.
    /// The key is the memory address, and the value is the corresponding freeze thread.
    /// </summary>
    private readonly Dictionary<IntPtr, Thread> freezeThreads = new Dictionary<IntPtr, Thread>();

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryEditor"/> class for the specified process.
    /// </summary>
    /// <param name="processName">The name of the target process to attach to.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the process cannot be opened or does not exist.
    /// </exception>
    /// <remarks>
    /// This constructor locates the specified process by its name and attempts to open it with all access permissions.
    /// If the process cannot be opened, an exception is thrown.
    /// </remarks>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     using var memoryEditor = new MemoryEditor("exampleProcess");
    ///     Console.WriteLine($"Attached to process: {memoryEditor.CurrentProcess.ProcessName}");
    /// }
    /// catch (InvalidOperationException ex)
    /// {
    ///     Console.WriteLine($"Failed to attach to process: {ex.Message}");
    /// }
    /// </code>
    /// </example>
    public MemoryEditor(string processName)
    {
        CurrentProcess = GetProcessByName(processName);
        hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, CurrentProcess.Id);

        if (hProcess == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Unable to open process: {processName}");
        }
    }

    /// <summary>
    /// Finalizer for the <see cref="MemoryEditor"/> class.
    /// </summary>
    /// <remarks>
    /// Ensures that unmanaged resources are released when the garbage collector reclaims the object. 
    /// This is a safeguard for cases where <see cref="Dispose"/> is not explicitly called.
    /// </remarks>
    ~MemoryEditor()
    {
        // Calls Dispose(false) to release unmanaged resources
        Dispose(false);
    }

    /// <summary>
    /// Provides indexed access for reading and writing values to specific memory addresses in the target process.
    /// </summary>
    /// <param name="address">The memory address to read from or write to.</param>
    /// <value>
    /// The value to write to the memory at the specified address. 
    /// Supported types include primitives, arrays, strings, and specific memory operations.
    /// </value>
    /// <exception cref="ArgumentNullException">Thrown when attempting to write a null value.</exception>
    /// <exception cref="NotSupportedException">Thrown when the provided type is not supported for memory operations.</exception>
    /// <example>
    /// <code>
    /// // Writing values
    /// memoryEditor[address] = 42;         // Write an integer
    /// memoryEditor[address] = 123.45f;    // Write a float
    /// memoryEditor[address] = "Hello";   // Write a string
    /// memoryEditor[address] = new byte[] { 0x90, 0x90 }; // Write a byte array
    /// memoryEditor[address] = (5, true); // Set the 5th bit to true
    ///
    /// // Reading values
    /// int intValue = memoryEditor[address].As<int>();
    /// float floatValue = memoryEditor[address].As<float>();
    /// string stringValue = memoryEditor[address].As<string>();
    /// </code>
    /// </example>
    public object this[IntPtr address]
    {
        get => new MemoryValueAccessor(this, address);
        set
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            switch (value)
            {
                case decimal dec:
                    SetDecimal(address, dec);
                    break;
                case byte b:
                    Set(address, b);
                    break;
                case short s:
                    Set(address, s);
                    break;
                case ushort us:
                    Set(address, us);
                    break;
                case int i:
                    Set(address, i);
                    break;
                case uint ui:
                    Set(address, ui);
                    break;
                case long l:
                    Set(address, l);
                    break;
                case ulong ul:
                    Set(address, ul);
                    break;
                case float f:
                    Set(address, f);
                    break;
                case double d:
                    Set(address, d);
                    break;
                case char c:
                    Set(address, c);
                    break;
                case byte[] bytes:
                    Set(address, bytes);
                    break;
                case string str when str.StartsWith("0x"):
                    SetHexString(address, str);
                    break;
                case string str:
                    Set(address, str, str.Length + 1);
                    break;
                case bool b:
                    SetBool(address, b);
                    break;
                case int[] ints:
                    SetArray(address, ints);
                    break;
                case float[] floats:
                    SetArray(address, floats);
                    break;
                case (int bitPosition, bool bitValue): // Bit manipulation
                    SetBit(address, bitPosition, bitValue);
                    break;
                default:
                    throw new NotSupportedException($"Type {value.GetType()} is not supported for memory write.");
            }
        }
    }

    /// <summary>
    /// Converts a binary string to a byte array.
    /// </summary>
    /// <param name="binaryString">The binary string to convert.</param>
    /// <returns>A byte array representing the binary data.</returns>
    /// <remarks>
    /// The binary string should only contain '0' and '1' characters. It automatically rounds up
    /// to the nearest byte if the string length is not a multiple of 8.
    /// </remarks>
    /// <example>
    /// <code>
    /// string binary = "11001100";
    /// byte[] bytes = ConvertBinaryStringToBytes(binary); // Result: { 0xCC }
    /// </code>
    /// </example>
    private byte[] ConvertBinaryStringToBytes(string binaryString)
    {
        int byteCount = (binaryString.Length + 7) / 8; // Round up to the nearest byte
        byte[] binaryData = new byte[byteCount];

        for (int i = 0; i < binaryString.Length; i++)
        {
            if (binaryString[i] == '1')
            {
                binaryData[i / 8] |= (byte)(1 << (7 - (i % 8)));
            }
        }

        return binaryData;
    }

    /// <summary>
    /// Converts a byte array to a binary string.
    /// </summary>
    /// <param name="bytes">The byte array to convert.</param>
    /// <returns>A string representing the binary data.</returns>
    /// <remarks>
    /// Each byte in the array is converted to its 8-bit binary representation.
    /// </remarks>
    /// <example>
    /// <code>
    /// byte[] bytes = { 0xCC };
    /// string binary = ConvertBytesToBinaryString(bytes); // Result: "11001100"
    /// </code>
    /// </example>
    private string ConvertBytesToBinaryString(byte[] bytes)
    {
        StringBuilder binaryString = new StringBuilder();
        foreach (byte b in bytes)
        {
            binaryString.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
        }
        return binaryString.ToString();
    }

    /// <summary>
    /// Finds a pointer chain from a base address to a target address.
    /// </summary>
    /// <param name="baseAddress">The starting base address of the pointer chain.</param>
    /// <param name="targetAddress">The target address to locate.</param>
    /// <param name="depth">The maximum depth of the pointer chain to search.</param>
    /// <returns>
    /// A list of offsets representing the pointer chain, or <c>null</c> if the chain cannot be resolved.
    /// </returns>
    /// <remarks>
    /// This method recursively traverses memory to resolve a multi-level pointer chain.
    /// </remarks>
    /// <example>
    /// <code>
    /// IntPtr baseAddr = new IntPtr(0x12340000);
    /// IntPtr targetAddr = new IntPtr(0x12345678);
    /// List<int> offsets = FindPointerChain(baseAddr, targetAddr, 5);
    /// </code>
    /// </example>
    private List<int> FindPointerChain(IntPtr baseAddress, IntPtr targetAddress, int depth)
    {
        if (depth <= 0) return null;

        byte[] buffer = ReadMemory(baseAddress, IntPtr.Size);
        if (buffer == null || buffer.Length != IntPtr.Size) return null;

        IntPtr nextAddress = IntPtr.Size == 4
            ? new IntPtr(BitConverter.ToInt32(buffer, 0))
            : new IntPtr(BitConverter.ToInt64(buffer, 0));

        if (nextAddress == targetAddress)
        {
            return new List<int> { 0 };
        }

        for (int offset = 0; offset < 0x1000; offset += 4)
        {
            IntPtr candidateAddress = IntPtr.Add(nextAddress, offset);
            if (candidateAddress == targetAddress)
            {
                return new List<int> { offset };
            }

            var deeperOffsets = FindPointerChain(candidateAddress, targetAddress, depth - 1);
            if (deeperOffsets != null)
            {
                deeperOffsets.Insert(0, offset);
                return deeperOffsets;
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves a process by its ID.
    /// </summary>
    /// <param name="processID">The ID of the process to retrieve.</param>
    /// <returns>
    /// A <see cref="Process"/> object representing the process with the specified ID, 
    /// or <c>null</c> if the process does not exist.
    /// </returns>
    /// <remarks>
    /// This method wraps the <see cref="Process.GetProcessById(int)"/> method to handle exceptions gracefully.
    /// </remarks>
    /// <example>
    /// <code>
    /// Process process = GetProcessByID(1234);
    /// if (process != null)
    /// {
    ///     Console.WriteLine($"Found process: {process.ProcessName}");
    /// }
    /// </code>
    /// </example>
    private static Process GetProcessByID(int processID)
    {
        try
        {
            return Process.GetProcessById(processID);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves a <see cref="Process"/> object by its name.
    /// </summary>
    /// <param name="processName">The name of the process to retrieve.</param>
    /// <returns>
    /// A <see cref="Process"/> object representing the first process that matches the specified name, or <c>null</c> if no matching process is found.
    /// </returns>
    /// <remarks>
    /// This method searches through all running processes and performs a case-insensitive comparison to find the specified process.
    /// If multiple processes share the same name, only the first one found will be returned.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if there are issues accessing the list of running processes.
    /// </exception>
    /// <example>
    /// <code>
    /// Process process = MemoryEditor.GetProcessByName("notepad");
    /// if (process != null)
    /// {
    ///     Console.WriteLine($"Found process: {process.ProcessName}, ID: {process.Id}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("Process not found.");
    /// }
    /// </code>
    /// </example>
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
    /// <summary>
    /// Retrieves writable memory regions in the target process.
    /// </summary>
    /// <returns>A list of tuples containing the start and end addresses of writable regions.</returns>
    private List<(IntPtr Start, IntPtr End)> GetWritableRegions()
    {
        List<(IntPtr Start, IntPtr End)> regions = new List<(IntPtr, IntPtr)>();
        IntPtr address = IntPtr.Zero;
        MEMORY_BASIC_INFORMATION mbi;

        while (VirtualQueryEx(hProcess, address, out mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) != 0)
        {
            if ((mbi.State == 0x1000) && // MEM_COMMIT
                (mbi.Protect & 0x04) != 0) // PAGE_READWRITE
            {
                regions.Add((mbi.BaseAddress, mbi.BaseAddress + (int)mbi.RegionSize));
            }

            address = mbi.BaseAddress + (int)mbi.RegionSize;
        }

        return regions;
    }

    /// <summary>
    /// Validates if a given string contains only binary characters ('0' or '1').
    /// </summary>
    /// <param name="str">The input string to validate.</param>
    /// <returns><c>true</c> if the string is binary; otherwise, <c>false</c>.</returns>
    private bool IsBinaryString(string str) => Regex.IsMatch(str, "^[01]+$");

    /// <summary>
    /// Checks if the specified memory range is valid and writable.
    /// </summary>
    /// <param name="address">The starting memory address.</param>
    /// <param name="length">The length of the memory range.</param>
    /// <returns><c>true</c> if the range is valid and writable; otherwise, <c>false</c>.</returns>
    private bool IsValidMemoryRange(IntPtr address, int length)
    {
        MEMORY_BASIC_INFORMATION mbi;
        return VirtualQueryEx(hProcess, address, out mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) != 0 &&
               mbi.State == 0x1000 && // MEM_COMMIT
               (mbi.Protect & 0x04) != 0; // PAGE_READWRITE
    }

    /// <summary>
    /// Logs a message to a file for debugging or informational purposes.
    /// </summary>
    /// <param name="message">The message to log.</param>
    private void Log(string message)
    {
        File.AppendAllText("memoryEditor.log", $"{DateTime.Now}: {message}\n");
    }

    /// <summary>
    /// Scans a memory chunk for pointers that match the target address and explores potential pointer chains.
    /// </summary>
    /// <param name="targetAddress">The address being searched for.</param>
    /// <param name="maxDepth">The maximum depth of pointer chains to explore.</param>
    /// <param name="rangeStart">The start of the memory range to scan.</param>
    /// <param name="rangeEnd">The end of the memory range to scan.</param>
    /// <param name="stepSize">The step size for scanning through memory.</param>
    /// <returns>A list of base addresses and their corresponding offset chains.</returns>
    private List<(IntPtr BaseAddress, List<int> Offsets)> ScanChunk(
        IntPtr targetAddress, int maxDepth, int rangeStart, int rangeEnd, int stepSize)
    {
        var results = new List<(IntPtr, List<int>)>();

        try
        {
            // Validate the range
            if (rangeStart >= rangeEnd || rangeEnd - rangeStart <= 0)
            {
                Console.WriteLine($"Invalid range: {rangeStart:X} to {rangeEnd:X}");
                return results;
            }

            // Read the specified memory range into a buffer
            byte[] buffer = ReadMemory((IntPtr)rangeStart, rangeEnd - rangeStart);
            if (buffer == null || buffer.Length == 0)
            {
                Console.WriteLine($"Failed to read memory range: {rangeStart:X} to {rangeEnd:X}");
                return results;
            }

            // Iterate through the buffer
            for (int i = 0; i < buffer.Length - IntPtr.Size; i += stepSize)
            {
                IntPtr candidate = IntPtr.Size == 4
                    ? new IntPtr(BitConverter.ToInt32(buffer, i))
                    : new IntPtr(BitConverter.ToInt64(buffer, i));

                // Check if the candidate matches the target address
                if (candidate == targetAddress)
                {
                    results.Add(((IntPtr)(rangeStart + i), new List<int>()));
                }
                else
                {
                    // Recursively search for pointer chains
                    var offsets = FindPointerChain(candidate, targetAddress, maxDepth - 1);
                    if (offsets != null)
                    {
                        results.Add(((IntPtr)(rangeStart + i), offsets));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning chunk {rangeStart:X} to {rangeEnd:X}: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Releases the resources used by the MemoryEditor instance.
    /// </summary>
    /// <param name="disposing">Indicates whether managed resources should be disposed.</param>
    /// <remarks>
    /// This method ensures that both managed and unmanaged resources are released properly. 
    /// Active freeze threads are safely terminated, and the process handle is closed.
    /// </remarks>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Release managed resources
                CurrentProcess = null;

                // Safely terminate all freeze threads
                foreach (var thread in freezeThreads.Values)
                {
                    try
                    {
                        if (thread.IsAlive)
                        {
                            thread.Abort(); // Use Abort as a last resort
                        }
                    }
                    catch (ThreadAbortException)
                    {
                        // Handle the thread abort gracefully
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error terminating thread: {ex.Message}");
                    }
                }

                freezeThreads.Clear();
            }

            // Release unmanaged resources
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    CloseHandle(hProcess);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing process handle: {ex.Message}");
                }
                finally
                {
                    hProcess = IntPtr.Zero;
                }
            }

            disposed = true;
        }
    }

    /// <summary>
    /// Retrieves the process ID of the current target process.
    /// </summary>
    /// <returns>
    /// An <see cref="int"/> representing the ID of the current process.
    /// </returns>
    /// <remarks>
    /// This method provides the process ID of the process currently being managed by the <see cref="MemoryEditor"/> instance.
    /// Ensure that the <c>CurrentProcess</c> property is properly initialized before calling this method.
    /// </remarks>
    /// <example>
    /// <code>
    /// var processID = memoryEditor.GetProcessID();
    /// Console.WriteLine($"Current Process ID: {processID}");
    /// </code>
    /// </example>
    internal int GetProcessID()
    {
        return CurrentProcess.Id;
    }

    /// <summary>
    /// Resolves a multi-level pointer chain to compute the final memory address.
    /// </summary>
    /// <param name="baseAddress">The starting base address from which the pointer chain begins.</param>
    /// <param name="offsets">A list of integer offsets to follow at each pointer level.</param>
    /// <returns>
    /// The resolved memory address after following all offsets in the pointer chain.
    /// </returns>
    /// <remarks>
    /// This method is commonly used for accessing dynamic memory structures, where a static base address points
    /// to dynamically allocated data through a chain of pointers. It reads the value at each address, applies
    /// the offsets, and follows the chain until the final address is determined.
    /// </remarks>
    internal IntPtr ResolvePointerChain(IntPtr relativeAddress, List<int> offsets = null)
    {
        IntPtr baseAddress = CurrentProcess.MainModule.BaseAddress;
        IntPtr currentAddress = IntPtr.Add(baseAddress, relativeAddress.ToInt32());
        Console.WriteLine($"Base Address: {baseAddress:X}, Relative Address: {relativeAddress:X}, Initial Address: {currentAddress:X}");

        if (offsets == null || offsets.Count == 0)
        {
            return currentAddress;
        }

        offsets.Reverse();

        foreach (var offset in offsets)
        {
            byte[] memoryBytes = ReadMemory(currentAddress, IntPtr.Size);
            if (memoryBytes == null || memoryBytes.Length != IntPtr.Size)
            {
                throw new Exception($"Failed to read memory at address {currentAddress:X}");
            }

            currentAddress = IntPtr.Size == 4
                ? new IntPtr(BitConverter.ToInt32(memoryBytes, 0))
                : new IntPtr(BitConverter.ToInt64(memoryBytes, 0));

            Console.WriteLine($"Pointer Value at Address {currentAddress:X}");
            currentAddress = IntPtr.Add(currentAddress, offset);
            Console.WriteLine($"New Address after Offset {offset:X}: {currentAddress:X}");
        }

        Console.WriteLine($"Final Resolved Address: {currentAddress:X}");
        return currentAddress;
    }

    /// <summary>
    /// Gets the handle to the target process's memory for performing read and write operations.
    /// </summary>
    /// <value>
    /// A <see cref="IntPtr"/> representing the handle to the target process.
    /// </value>
    /// <remarks>
    /// This handle is required for interacting with the process's memory space. 
    /// The handle is initialized when the MemoryEditor is constructed and is automatically released during disposal.
    /// </remarks>
    internal static IntPtr hProcess { get; private set; }

    /// <summary>
    /// Attaches a debugger to a process with the specified ID.
    /// </summary>
    /// <param name="processId">The ID of the process to which the debugger will be attached.</param>
    /// <remarks>
    /// This method prompts the user for confirmation before attaching the debugger. 
    /// If confirmed, the debugger is launched, and a breakpoint is set.
    /// </remarks>
    /// <exception cref="Exception">Thrown if an error occurs while attaching the debugger.</exception>
    public void AttachDebugger(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);

            // Create the confirmation dialog using MessageBox
            MessageBoxResult result = MessageBox.Show("Are you sure you want to attach the debugger to this process?",
                                                      "Confirmation",
                                                      MessageBoxButton.YesNo,
                                                      MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Attach the debugger
                Debugger.Launch();
                Debugger.Break();

                // Show a success message
                MessageBox.Show($"Debugger attached to process: {process.ProcessName} (ID: {process.Id})");
            }
        }
        catch (Exception ex)
        {
            // Show an error message if there is an issue
            MessageBox.Show($"Error attaching debugger: {ex.Message}");
        }
    }

    /// <summary>
    /// Automatically determine the pointer chain depth by resolving pointers until a static address is reached.
    /// </summary>
    /// <param name="targetAddress"></param>
    /// <param name="maxDepth"></param>
    /// <returns></returns>
    public List<int> AutoResolvePointerDepth(IntPtr targetAddress, int maxDepth = 5)
    {
        List<int> offsets = new List<int>();
        IntPtr currentAddress = targetAddress;

        for (int depth = 0; depth < maxDepth; depth++)
        {
            byte[] memoryBytes = ReadMemory(currentAddress, IntPtr.Size);
            if (memoryBytes == null || memoryBytes.Length != IntPtr.Size)
            {
                break;
            }

            IntPtr nextAddress = IntPtr.Size == 4
                ? new IntPtr(BitConverter.ToInt32(memoryBytes, 0))
                : new IntPtr(BitConverter.ToInt64(memoryBytes, 0));

            offsets.Add(IntPtr.Subtract(nextAddress, (int)currentAddress).ToInt32());
            currentAddress = nextAddress;
        }

        return offsets;
    }

    /// <summary>
    /// Compares the memory at a specified address with an expected value.
    /// </summary>
    /// <param name="address">The memory address to compare.</param>
    /// <param name="expectedValue">The expected byte array to compare against.</param>
    /// <returns>True if the memory matches the expected value; otherwise, false.</returns>
    public bool CompareMemory(IntPtr address, byte[] expectedValue)
    {
        byte[] memoryBytes = ReadMemory(address, expectedValue.Length);
        return memoryBytes != null && memoryBytes.SequenceEqual(expectedValue);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="MemoryEditor"/> instance.
    /// </summary>
    /// <remarks>
    /// This method calls the <see cref="Dispose(bool)"/> method to release both managed and unmanaged resources.
    /// It suppresses the finalizer to optimize garbage collection.
    /// </remarks>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this); // Prevent finalizer from being called
    }

    /// <summary>
    /// Continuously writes a specified value to a memory region, effectively freezing its value.
    /// </summary>
    /// <param name="startAddress">The starting address of the memory region.</param>
    /// <param name="size">The size of the memory region in bytes.</param>
    /// <param name="freezeValue">The value to freeze the memory region with.</param>
    /// <remarks>
    /// This method runs on a background thread to repeatedly write the value to the memory region at 50ms intervals.
    /// </remarks>
    public void FreezeRegion(IntPtr startAddress, int size, byte[] freezeValue)
    {
        Thread freezeThread = new Thread(() =>
        {
            while (true)
            {
                for (int offset = 0; offset < size; offset += freezeValue.Length)
                {
                    WriteMemory(IntPtr.Add(startAddress, offset), freezeValue, (uint)freezeValue.Length, out _);
                }
                Thread.Sleep(50);
            }
        });

        freezeThread.IsBackground = true;
        freezeThread.Start();
    }

    /// <summary>
    /// Continuously writes a specified value to a memory address, effectively "freezing" the value.
    /// </summary>
    /// <param name="address">The memory address where the value will be frozen.</param>
    /// <param name="value">The byte array representing the value to be frozen at the specified address.</param>
    /// <remarks>
    /// This method starts a background thread that repeatedly writes the specified value to the target memory address
    /// at regular intervals (default is 50ms). If a freeze operation is already active for the address, the method exits
    /// without creating a new thread. The freezing operation can be stopped using the <see cref="UnfreezeValue"/> method.
    /// </remarks>
    /// <example>
    /// <code>
    /// IntPtr targetAddress = new IntPtr(0x12345678);
    /// byte[] valueToFreeze = BitConverter.GetBytes(123.45f); // Example for freezing a float value
    /// memoryEditor.FreezeValue(targetAddress, valueToFreeze);
    /// </code>
    /// </example>
    public void FreezeValue(IntPtr address, byte[] value)
    {
        if (freezeThreads.ContainsKey(address))
        {
            Console.WriteLine($"Address {address:X} is already being frozen.");
            return;
        }

        Thread freezeThread = new Thread(() =>
        {
            while (true)
            {
                uint bytesWritten;
                WriteMemory(address, value, (uint)value.Length, out bytesWritten);
                Thread.Sleep(50); // Prevent excessive CPU usage
            }
        });

        freezeThread.IsBackground = true;
        freezeThread.Start();

        freezeThreads[address] = freezeThread;
        Console.WriteLine($"Started freezing value at address {address:X}.");
    }

    /// <summary>
    /// Reads a value of the specified type from memory.
    /// </summary>
    /// <typeparam name="T">The type of the value to read.</typeparam>
    /// <param name="address">The memory address to read from.</param>
    /// <param name="length">The number of bytes to read (optional for variable-length types).</param>
    /// <returns>The value read from memory, cast to the specified type.</returns>
    /// <exception cref="NotSupportedException">Thrown if the specified type is not supported.</exception>
    public T Get<T>(IntPtr address, int length = 0)
    {
        byte[] memoryBytes = length > 0
            ? ReadMemory(address, length)
            : ReadMemory(address, Marshal.SizeOf<T>());

        if (typeof(T) == typeof(byte)) return (T)(object)memoryBytes[0];
        if (typeof(T) == typeof(short)) return (T)(object)BitConverter.ToInt16(memoryBytes, 0);
        if (typeof(T) == typeof(ushort)) return (T)(object)BitConverter.ToUInt16(memoryBytes, 0);
        if (typeof(T) == typeof(int)) return (T)(object)BitConverter.ToInt32(memoryBytes, 0);
        if (typeof(T) == typeof(uint)) return (T)(object)BitConverter.ToUInt32(memoryBytes, 0);
        if (typeof(T) == typeof(long)) return (T)(object)BitConverter.ToInt64(memoryBytes, 0);
        if (typeof(T) == typeof(ulong)) return (T)(object)BitConverter.ToUInt64(memoryBytes, 0);
        if (typeof(T) == typeof(float)) return (T)(object)BitConverter.ToSingle(memoryBytes, 0);
        if (typeof(T) == typeof(double)) return (T)(object)BitConverter.ToDouble(memoryBytes, 0);
        if (typeof(T) == typeof(char)) return (T)(object)(char)memoryBytes[0];
        if (typeof(T) == typeof(byte[])) return (T)(object)memoryBytes;
        if (typeof(T) == typeof(string))
        {
            int nullTerminatorIndex = Array.IndexOf(memoryBytes, (byte)0);
            return (T)(object)Encoding.UTF8.GetString(memoryBytes, 0, nullTerminatorIndex >= 0 ? nullTerminatorIndex : memoryBytes.Length);
        }

        throw new NotSupportedException($"Type {typeof(T)} is not supported for memory read.");
    }

    /// <summary>
    /// Retrieves a list of all running applications on the system.
    /// </summary>
    /// <returns>A list of <see cref="ProcessInfo"/> objects representing the running applications.</returns>
    public List<ProcessInfo> GetApplications()
    {
        var applications = new HashSet<string>();
        var processInfos = new List<ProcessInfo>();

        foreach (var process in Process.GetProcesses())
        {
            string appName = $"{process.Id:X8}- {Path.GetFileNameWithoutExtension(process.ProcessName)}";
            if (applications.Add(appName))
            {
                processInfos.Add(new ProcessInfo
                {
                    Id = process.Id,
                    Name = appName,
                    Type = "Application"
                });
            }
        }

        return processInfos;
    }

    /// <summary>
    /// Reads an array of structures from memory.
    /// </summary>
    /// <typeparam name="T">The type of the structures to read.</typeparam>
    /// <param name="address">The starting memory address to read from.</param>
    /// <param name="length">The number of structures to read.</param>
    /// <returns>An array of structures read from memory.</returns>
    public T[] GetArray<T>(IntPtr address, int length) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] memoryBytes = ReadMemory(address, size * length);

        T[] result = new T[length];
        Buffer.BlockCopy(memoryBytes, 0, result, 0, memoryBytes.Length);
        return result;
    }

    /// <summary>
    /// Reads the value of a specific bit at a given memory address and position.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <param name="bitPosition">The bit position (0-7 for byte, or 0-31 for int).</param>
    /// <returns>A boolean indicating whether the bit is set (true) or not (false).</returns>
    public bool GetBit(IntPtr address, int bitPosition)
    {
        if (bitPosition < 0 || bitPosition > 7)
            throw new ArgumentOutOfRangeException(nameof(bitPosition), "Bit position must be between 0 and 7.");

        byte value = Get<byte>(address); // Read the byte from memory
        return (value & (1 << bitPosition)) != 0;
    }

    /// <summary>
    /// Reads a boolean value from memory.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <returns><c>true</c> if the value is non-zero; otherwise, <c>false</c>.</returns>
    public bool GetBool(IntPtr address)
    {
        byte value = Get<byte>(address);
        return value != 0;
    }

    /// <summary>
    /// Reads an array of complex structures from memory.
    /// </summary>
    /// <typeparam name="T">The type of the structure.</typeparam>
    /// <param name="address">The starting address of the array in memory.</param>
    /// <param name="count">The number of elements in the array.</param>
    /// <returns>An array of the specified type read from memory.</returns>
    public T[] GetComplexArray<T>(IntPtr address, int count) where T : struct
    {
        T[] array = new T[count];
        int size = Marshal.SizeOf<T>();

        for (int i = 0; i < count; i++)
        {
            array[i] = GetStruct<T>(address + (i * size));
        }
        return array;
    }

    /// <summary>
    /// Reads a <see cref="decimal"/> value from memory.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <returns>The <see cref="decimal"/> value read from memory.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the memory read fails or the size is incorrect.</exception>
    public decimal GetDecimal(IntPtr address)
    {
        byte[] memoryBytes = ReadMemory(address, 16); // `decimal` requires 16 bytes.
        if (memoryBytes == null || memoryBytes.Length != 16)
        {
            throw new InvalidOperationException("Unable to read decimal value from memory.");
        }

        // Use `decimal`'s internal structure for manual conversion
        int[] bits = new int[4];
        for (int i = 0; i < 4; i++)
        {
            bits[i] = BitConverter.ToInt32(memoryBytes, i * 4);
        }
        return new decimal(bits);
    }

    /// <summary>
    /// Reads a hexadecimal string representation of a memory range.
    /// </summary>
    /// <param name="address">The memory address to start reading from.</param>
    /// <param name="length">The number of bytes to read.</param>
    /// <returns>A string representing the hexadecimal values.</returns>
    public string GetHexString(IntPtr address, int length)
    {
        byte[] buffer = Get<byte[]>(address, length);
        return BitConverter.ToString(buffer).Replace("-", string.Empty);
    }

    /// <summary>
    /// Reads a platform-specific integer from memory.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <returns>The platform-specific integer value.</returns>
    public nint GetNint(IntPtr address)
    {
        return IntPtr.Size == 4
            ? (nint)Get<int>(address)
            : (nint)Get<long>(address);
    }

    /// <summary>
    /// Reads a platform-specific unsigned integer from memory.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <returns>The platform-specific unsigned integer value.</returns>
    public nuint GetNuint(IntPtr address)
    {
        return IntPtr.Size == 4
            ? (nuint)Get<uint>(address)
            : (nuint)Get<ulong>(address);
    }

    /// <summary>
    /// Reads a pointer value from memory.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <returns>The pointer value read from memory.</returns>
    public IntPtr GetPointer(IntPtr address)
    {
        byte[] memoryBytes = ReadMemory(address, IntPtr.Size);
        return IntPtr.Size == 4
            ? new IntPtr(BitConverter.ToInt32(memoryBytes, 0))
            : new IntPtr(BitConverter.ToInt64(memoryBytes, 0));
    }

    /// <summary>
    /// Reads a string from memory with a specified maximum length and encoding.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <param name="maxLength">The maximum length of the string.</param>
    /// <param name="encoding">The encoding to use for the string.</param>
    /// <returns>The string read from memory.</returns>
    public string GetString(IntPtr address, int maxLength, Encoding encoding)
    {
        byte[] buffer = ReadMemory(address, maxLength);
        if (buffer == null || buffer.Length == 0)
        {
            throw new InvalidOperationException("Unable to read string value from memory.");
        }

        int nullTerminatorIndex = Array.IndexOf(buffer, (byte)0);
        if (nullTerminatorIndex >= 0)
        {
            return encoding.GetString(buffer, 0, nullTerminatorIndex);
        }
        return encoding.GetString(buffer);
    }

    /// <summary>
    /// Reads an array of strings from memory.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <param name="count">The number of strings to read.</param>
    /// <param name="maxLength">The maximum length of each string.</param>
    /// <returns>An array of strings read from memory.</returns>
    public string[] GetStringArray(IntPtr address, int count, int maxLength)
    {
        string[] result = new string[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = Get<string>(address + (i * maxLength), maxLength);
        }
        return result;
    }

    /// <summary>
    /// Reads a structure of type <typeparamref name="T"/> from memory.
    /// </summary>
    /// <typeparam name="T">The type of the structure.</typeparam>
    /// <param name="address">The memory address to read from.</param>
    /// <returns>The structure read from memory.</returns>
    public T GetStruct<T>(IntPtr address) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] memoryBytes = ReadMemory(address, size);

        GCHandle handle = GCHandle.Alloc(memoryBytes, GCHandleType.Pinned);
        T result = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        handle.Free();

        return result;
    }

    /// <summary>
    /// Parses a cheat table XML file and returns a list of cheat entries.
    /// </summary>
    /// <param name="FilePath">The path to the cheat table file.</param>
    /// <returns>A list of <see cref="CheatEntry"/> objects.</returns>
    public static List<CheatEntry> ParseCheatTable(string FilePath)
    {
        var cheatEntries = new List<CheatEntry>();
        string xmlContent = File.ReadAllText(FilePath);
        var document = XDocument.Parse(xmlContent);
        var entries = document.Descendants("CheatEntry");

        foreach (var entry in entries)
        {
            var cheatEntry = new CheatEntry
            {
                ID = int.Parse(entry.Element("ID")?.Value ?? "0"),
                Description = entry.Element("Description")?.Value.Trim('"'),
                ShowAsSigned = entry.Element("ShowAsSigned")?.Value == "1",
                Type = entry.Element("VariableType")?.Value,
                Address = entry.Element("Address")?.Value
            };

            var offsets = entry.Element("Offsets")?.Elements("Offset");
            if (offsets != null)
            {
                foreach (var offset in offsets)
                {
                    cheatEntry.Offsets.Add(Convert.ToInt32(offset.Value, 16));
                }
            }

            cheatEntries.Add(cheatEntry);
        }

        return cheatEntries;
    }
    /// <summary>
    /// Performs a pointer scan to locate memory addresses pointing to the specified target address.
    /// </summary>
    /// <param name="targetAddress">The target memory address to scan for.</param>
    /// <param name="maxDepth">The maximum depth of pointer chains to follow.</param>
    /// <param name="rangeStart">The start address of the memory range to scan.</param>
    /// <param name="rangeEnd">The end address of the memory range to scan.</param>
    /// <param name="stepSize">The step size for scanning memory (default is 4 bytes).</param>
    /// <returns>A list of base addresses and pointer offsets leading to the target address.</returns>
    public List<(IntPtr BaseAddress, List<int> Offsets)> PointerScan(IntPtr targetAddress, int maxDepth, int rangeStart, int rangeEnd, int stepSize = 4)
    {
        int numThreads = Environment.ProcessorCount; // Use all available cores
        int chunkSize = (rangeEnd - rangeStart) / numThreads;

        List<(IntPtr BaseAddress, List<int> Offsets)> results = new List<(IntPtr, List<int>)>();
        List<Task<List<(IntPtr, List<int>)>>> tasks = new List<Task<List<(IntPtr, List<int>)>>>();

        for (int i = 0; i < numThreads; i++)
        {
            int start = rangeStart + (i * chunkSize);
            int end = (i == numThreads - 1) ? rangeEnd : start + chunkSize;

            tasks.Add(Task.Run(() => ScanChunk(targetAddress, maxDepth, start, end, stepSize)));
        }

        Task.WaitAll(tasks.ToArray());

        foreach (var task in tasks)
        {
            results.AddRange(task.Result);
        }

        return results;
    }

    /// <summary>
    /// Reads a specified number of bytes from the target process's memory starting at a given address.
    /// </summary>
    /// <param name="address">The starting memory address in the target process to read from.</param>
    /// <param name="length">The total number of bytes to read from memory.</param>
    /// <returns>
    /// A byte array containing the data read from the target memory. Returns <c>null</c> if the process handle is invalid or the read operation fails.
    /// </returns>
    /// <remarks>
    /// This method reads data in chunks, writing each chunk to a memory stream, which is then returned as a consolidated byte array.
    /// It handles cases where large reads are required by splitting them into manageable chunks, determined by <c>MAX_CHUNK_SIZE</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the process handle is not valid or memory cannot be accessed.
    /// </exception>
    /// <example>
    /// <code>
    /// IntPtr targetAddress = new IntPtr(0x12345678);
    /// int length = 256; // Read 256 bytes
    /// byte[] data = ReadMemory(targetAddress, length);
    /// 
    /// if (data != null)
    /// {
    ///     Console.WriteLine($"Successfully read {data.Length} bytes from {targetAddress:X}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine($"Failed to read memory at {targetAddress:X}");
    /// }
    /// </code>
    /// </example>
    public byte[] ReadMemory(IntPtr address, int length)
    {
        if (hProcess != IntPtr.Zero)
        {
            byte[] buffer = new byte[MAX_CHUNK_SIZE];
            int totalBytesRead = 0;
            int bytesRead;

            using (var memoryStream = new MemoryStream())
            {
                while (totalBytesRead < length)
                {
                    int chunkSize = Math.Min(MAX_CHUNK_SIZE, length - totalBytesRead);
                    if (ReadProcessMemory(hProcess, address + totalBytesRead, buffer, (uint)chunkSize, out bytesRead))
                    {
                        memoryStream.Write(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                    }
                    else
                    {
                        break;
                    }
                }

                CloseHandle(hProcess);
                return memoryStream.ToArray();
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a pointer chain to compute the final memory address.
    /// </summary>
    /// <param name="baseAddress">The starting address of the pointer chain.</param>
    /// <param name="offsets">A list of offsets to follow in the pointer chain.</param>
    /// <returns>The resolved memory address after following all offsets.</returns>
    /// <exception cref="InvalidOperationException">Thrown if memory reading fails during the pointer resolution.</exception>
    public IntPtr ResolvePointer(IntPtr baseAddress, List<int> offsets)
    {
        IntPtr currentAddress = baseAddress;
        foreach (int offset in offsets)
        {
            byte[] memoryBytes = ReadMemory(currentAddress, IntPtr.Size);
            if (memoryBytes == null || memoryBytes.Length != IntPtr.Size)
            {
                throw new InvalidOperationException($"Failed to resolve pointer at address {currentAddress:X}");
            }

            currentAddress = IntPtr.Size == 4
                ? new IntPtr(BitConverter.ToInt32(memoryBytes, 0))
                : new IntPtr(BitConverter.ToInt64(memoryBytes, 0));
            currentAddress = IntPtr.Add(currentAddress, offset);
        }

        return currentAddress;
    }

    /// <summary>
    /// Resolves a pointer chain with detailed output for each step in the chain.
    /// </summary>
    /// <param name="baseAddress">The starting address of the pointer chain.</param>
    /// <param name="offsets">A list of offsets to follow in the pointer chain.</param>
    /// <returns>
    /// A list of tuples containing each address and its corresponding offset during the resolution.
    /// </returns>
    /// <exception cref="Exception">Thrown if memory reading fails during pointer resolution.</exception>
    public List<(IntPtr Address, int Offset)> ResolvePointerChainDetailed(IntPtr baseAddress, List<int> offsets)
    {
        List<(IntPtr Address, int Offset)> resolutionPath = new List<(IntPtr, int)>();

        IntPtr currentAddress = baseAddress;
        resolutionPath.Add((currentAddress, 0));

        foreach (var offset in offsets)
        {
            byte[] memoryBytes = ReadMemory(currentAddress, IntPtr.Size);
            if (memoryBytes == null || memoryBytes.Length != IntPtr.Size)
            {
                throw new Exception($"Failed to read memory at address {currentAddress:X}");
            }

            currentAddress = IntPtr.Size == 4
                ? new IntPtr(BitConverter.ToInt32(memoryBytes, 0))
                : new IntPtr(BitConverter.ToInt64(memoryBytes, 0));

            currentAddress = IntPtr.Add(currentAddress, offset);
            resolutionPath.Add((currentAddress, offset));
        }

        return resolutionPath;
    }

    /// <summary>
    /// Scans memory for pointer chains that lead to a specific target address.
    /// </summary>
    /// <param name="targetAddress">The memory address to locate.</param>
    /// <param name="maxDepth">The maximum depth of pointer chains to follow.</param>
    /// <param name="rangeStart">The starting address of the memory range to scan.</param>
    /// <param name="rangeEnd">The ending address of the memory range to scan.</param>
    /// <param name="stepSize">The step size for scanning memory (default is 4 bytes).</param>
    /// <returns>
    /// A list of base addresses and offsets leading to the target address.
    /// </returns>
    public List<(IntPtr BaseAddress, List<int> Offsets)> ScanMemory(IntPtr targetAddress, int maxDepth, int rangeStart, int rangeEnd, int stepSize = 4)
    {
        int numThreads = Environment.ProcessorCount;
        int chunkSize = (rangeEnd - rangeStart) / numThreads;

        List<Task<List<(IntPtr BaseAddress, List<int> Offsets)>>> tasks = new List<Task<List<(IntPtr, List<int>)>>>();

        for (int i = 0; i < numThreads; i++)
        {
            int start = rangeStart + (i * chunkSize);
            int end = (i == numThreads - 1) ? rangeEnd : start + chunkSize;

            tasks.Add(Task.Run(() => ScanChunk(targetAddress, maxDepth, start, end, stepSize)));
        }

        Task.WaitAll(tasks.ToArray());
        return tasks.SelectMany(task => task.Result).ToList();
    }

    /// <summary>
    /// Writes a value of type <typeparamref name="T"/> to a specific memory address.
    /// </summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="length">
    /// The maximum length of the value in bytes (optional, defaults to 0, which means the size of the type is used).
    /// </param>
    /// <exception cref="NotSupportedException">Thrown if the type <typeparamref name="T"/> is not supported for writing.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the memory write operation fails.</exception>
    public void Set<T>(IntPtr address, T value, int length = 0)
    {
        byte[] buffer;

        if (typeof(T) == typeof(byte)) buffer = new[] { (byte)(object)value };
        else if (typeof(T) == typeof(short)) buffer = BitConverter.GetBytes((short)(object)value);
        else if (typeof(T) == typeof(ushort)) buffer = BitConverter.GetBytes((ushort)(object)value);
        else if (typeof(T) == typeof(int)) buffer = BitConverter.GetBytes((int)(object)value);
        else if (typeof(T) == typeof(uint)) buffer = BitConverter.GetBytes((uint)(object)value);
        else if (typeof(T) == typeof(long)) buffer = BitConverter.GetBytes((long)(object)value);
        else if (typeof(T) == typeof(ulong)) buffer = BitConverter.GetBytes((ulong)(object)value);
        else if (typeof(T) == typeof(float)) buffer = BitConverter.GetBytes((float)(object)value);
        else if (typeof(T) == typeof(double)) buffer = BitConverter.GetBytes((double)(object)value);
        else if (typeof(T) == typeof(char)) buffer = new[] { (byte)(object)value };
        else if (typeof(T) == typeof(byte[])) buffer = (byte[])(object)value;
        else if (typeof(T) == typeof(string))
        {
            buffer = Encoding.UTF8.GetBytes((string)(object)value);
            if (buffer.Length > length - 1) throw new ArgumentException("String exceeds specified length.");
            Array.Resize(ref buffer, length);
        }
        else if (typeof(T) == typeof(bool)) buffer = new[] { (byte)((bool)(object)value ? 1 : 0) };
        else throw new NotSupportedException($"Type {typeof(T)} is not supported for memory write.");

        uint bytesWritten;
        if (!WriteMemory(address, buffer, (uint)buffer.Length, out bytesWritten))
        {
            throw new InvalidOperationException($"Failed to write value to memory at {address:X}.");
        }
    }

    /// <summary>
    /// Writes an array of values to memory.
    /// </summary>
    /// <typeparam name="T">The type of the array elements.</typeparam>
    /// <param name="address">The starting memory address to write to.</param>
    /// <param name="values">The array of values to write.</param>
    public void SetArray<T>(IntPtr address, T[] values) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] buffer = new byte[values.Length * size];
        Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
        Set(address, buffer);
    }

    /// <summary>
    /// Sets or clears a specific bit at a given memory address and position.
    /// </summary>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="bitPosition">The bit position (0-7 for byte, or 0-31 for int).</param>
    /// <param name="bitValue">True to set the bit, false to clear it.</param>
    /// <summary>
    /// Sets or clears a specific bit at the given memory address.
    /// </summary>
    /// <param name="address">The memory address containing the target byte.</param>
    /// <param name="bitPosition">The position of the bit to modify (0-7).</param>
    /// <param name="bitValue">True to set the bit, false to clear it.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the bit position is outside the valid range (0-7).</exception>
    public void SetBit(IntPtr address, int bitPosition, bool bitValue)
    {
        if (bitPosition < 0 || bitPosition > 7)
            throw new ArgumentOutOfRangeException(nameof(bitPosition), "Bit position must be between 0 and 7.");

        byte currentValue = Get<byte>(address); // Read the byte from memory
        byte newValue = bitValue
            ? (byte)(currentValue | (1 << bitPosition)) // Set the bit
            : (byte)(currentValue & ~(1 << bitPosition)); // Clear the bit

        Set(address, newValue); // Write the updated byte back to memory
    }

    /// <summary>
    /// Sets a boolean value at the specified memory address.
    /// </summary>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="value">The boolean value to write.</param>
    public void SetBool(IntPtr address, bool value)
    {
        Set(address, value);
    }

    /// <summary>
    /// Writes a complex array of structures to the specified memory address.
    /// </summary>
    /// <typeparam name="T">The type of the structure.</typeparam>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="values">The array of structures to write.</param>
    public void SetComplexArray<T>(IntPtr address, T[] values) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        for (int i = 0; i < values.Length; i++)
        {
            SetStruct(address + (i * size), values[i]);
        }
    }

    /// <summary>
    /// Writes a decimal value to the specified memory address.
    /// </summary>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="value">The decimal value to write.</param>
    public void SetDecimal(IntPtr address, decimal value)
    {
        int[] bits = decimal.GetBits(value); // Convert `decimal` to its 4-part integer representation.
        byte[] buffer = new byte[16];
        for (int i = 0; i < 4; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(bits[i]), 0, buffer, i * 4, 4);
        }
        Set(address, buffer);
    }

    /// <summary>
    /// Writes a hexadecimal string to the specified memory address.
    /// </summary>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="hexString">The hexadecimal string to convert and write.</param>
    /// <exception cref="ArgumentException">Thrown if the hexadecimal string is invalid.</exception>
    public void SetHexString(IntPtr address, string hexString)
    {
        if (string.IsNullOrWhiteSpace(hexString) || !Regex.IsMatch(hexString, @"\A\b(0[xX])?[0-9a-fA-F]+\b\Z"))
            throw new ArgumentException("Invalid hexadecimal string.");

        hexString = hexString.StartsWith("0x") ? hexString.Substring(2) : hexString;

        int byteCount = hexString.Length / 2;
        byte[] buffer = new byte[byteCount];
        for (int i = 0; i < byteCount; i++)
        {
            buffer[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
        }

        Set(address, buffer);
    }

    /// <summary>
    /// Writes a native integer (nint) value to the specified memory address.
    /// </summary>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="value">The native integer value to write.</param>
    public void SetNint(IntPtr address, nint value)
    {
        if (IntPtr.Size == 4)
            Set(address, (int)value);
        else
            Set(address, (long)value);
    }

    /// <summary>
    /// Writes an unsigned native integer (nuint) value to the specified memory address.
    /// </summary>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="value">The unsigned native integer value to write.</param>
    public void SetNuint(IntPtr address, nuint value)
    {
        if (IntPtr.Size == 4)
            Set(address, (uint)value);
        else
            Set(address, (ulong)value);
    }

    /// <summary>
    /// Writes a pointer value to the specified memory address.
    /// </summary>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="pointerValue">The pointer value to write.</param>
    public void SetPointer(IntPtr address, IntPtr pointerValue)
    {
        if (IntPtr.Size == 4)
            Set(address, pointerValue.ToInt32());
        else
            Set(address, pointerValue.ToInt64());
    }

    /// <summary>
    /// Writes a string value to the specified memory address using a specified encoding.
    /// </summary>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="value">The string value to write.</param>
    /// <param name="encoding">The encoding to use for the string.</param>
    /// <param name="maxLength">The maximum allowed length of the string.</param>
    /// <exception cref="ArgumentException">Thrown if the string exceeds the specified maximum length.</exception>
    public void SetString(IntPtr address, string value, Encoding encoding, int maxLength)
    {
        byte[] stringBytes = encoding.GetBytes(value);
        if (stringBytes.Length > maxLength - 1)
        {
            throw new ArgumentException("The string is too long for the specified memory length.");
        }

        byte[] buffer = new byte[maxLength];
        Array.Copy(stringBytes, buffer, stringBytes.Length);
        buffer[stringBytes.Length] = 0; // Null terminator

        WriteMemory(address, buffer, (uint)buffer.Length, out _);
    }

    /// <summary>
    /// Writes an array of strings to the specified memory address.
    /// </summary>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="values">The array of strings to write.</param>
    /// <param name="maxLength">The maximum length of each string.</param>
    public void SetStringArray(IntPtr address, string[] values, int maxLength)
    {
        for (int i = 0; i < values.Length; i++)
        {
            Set(address + (i * maxLength), values[i], maxLength);
        }
    }

    /// <summary>
    /// Writes a structure value to the specified memory address.
    /// </summary>
    /// <typeparam name="T">The type of the structure.</typeparam>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="value">The structure value to write.</param>
    public void SetStruct<T>(IntPtr address, T value) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] buffer = new byte[size];

        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(value, ptr, true);
        Marshal.Copy(ptr, buffer, 0, size);
        Marshal.FreeHGlobal(ptr);

        Set(address, buffer);
    }

    /// <summary>
    /// Stops freezing a value at the specified memory address.
    /// </summary>
    /// <param name="address">The memory address where freezing should be stopped.</param>
    /// <remarks>
    /// This method looks up the address in the internal collection of freeze threads. If a freeze thread is found,
    /// it is terminated, and the address is removed from the collection. If no freeze thread exists for the address,
    /// a message is displayed indicating that no freezing was active for the given address.
    /// </remarks>
    /// <example>
    /// <code>
    /// IntPtr targetAddress = new IntPtr(0x12345678);
    /// memoryEditor.UnfreezeValue(targetAddress);
    /// </code>
    /// </example>
    public void UnfreezeValue(IntPtr address)
    {
        if (freezeThreads.TryGetValue(address, out Thread freezeThread))
        {
            freezeThread.Abort();
            freezeThreads.Remove(address);
            Console.WriteLine($"Stopped freezing value at address {address:X}.");
        }
        else
        {
            Console.WriteLine($"No freezing thread found for address {address:X}.");
        }
    }

    /// <summary>
    /// validate if a given pointer chain is valid (i.e., leads to the expected value).
    /// </summary>
    /// <param name="baseAddress"></param>
    /// <param name="offsets"></param>
    /// <param name="expectedAddress"></param>
    /// <returns></returns>
    public bool ValidatePointerChain(IntPtr baseAddress, List<int> offsets, IntPtr expectedAddress)
    {
        IntPtr resolvedAddress = ResolvePointerChain(baseAddress, offsets);
        return resolvedAddress == expectedAddress;
    }

    /// <summary>
    /// Allow resolving chains with multiple levels of offsets and display the entire chain step-by-step.
    /// </summary>
    /// <param name="baseAddress"></param>
    /// <param name="offsets"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    /// <summary>
    /// Writes a specified byte array to the target process's memory at a given address.
    /// </summary>
    /// <param name="lpBaseAddress">The memory address in the target process where data will be written.</param>
    /// <param name="lpBuffer">The byte array containing the data to write into memory.</param>
    /// <param name="nSize">The size of the buffer to write, in bytes.</param>
    /// <param name="lpNumberOfBytesWritten">
    /// Outputs the actual number of bytes successfully written to the target memory.
    /// </param>
    /// <returns>
    /// A boolean indicating whether the memory write operation was successful.
    /// </returns>
    /// <remarks>
    /// This method temporarily changes the memory protection of the target address to ensure
    /// the write operation is allowed, then restores the original protection after writing.
    /// It also ensures that the process handle is valid before attempting the operation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the process handle is not valid or memory cannot be accessed.
    /// </exception>
    /// <example>
    /// <code>
    /// IntPtr targetAddress = new IntPtr(0x12345678);
    /// byte[] data = { 0x90, 0x90 }; // NOP instructions
    /// uint bytesWritten;
    /// bool success = WriteMemory(targetAddress, data, (uint)data.Length, out bytesWritten);
    /// 
    /// if (success)
    /// {
    ///     Console.WriteLine($"Successfully wrote {bytesWritten} bytes to {targetAddress:X}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine($"Failed to write memory at {targetAddress:X}");
    /// }
    /// </code>
    /// </example>
    public static bool WriteMemory(IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out uint lpNumberOfBytesWritten)
    {
        if (hProcess != IntPtr.Zero)
        {
            try
            {
                uint previousAccessProtect = 0;
                VirtualProtectEx(hProcess, lpBaseAddress, nSize, PAGE_EXECUTE_READWRITE, out previousAccessProtect);
                bool result = WriteProcessMemory(hProcess, lpBaseAddress, lpBuffer, nSize, out lpNumberOfBytesWritten);
                VirtualProtectEx(hProcess, lpBaseAddress, nSize, previousAccessProtect, out previousAccessProtect);
                return result;
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        else
        {
            lpNumberOfBytesWritten = 0;
            return false;
        }
    }

    /// <summary>
    /// Gets or sets the current target process being managed by the MemoryEditor.
    /// </summary>
    /// <value>
    /// A <see cref="Process"/> instance representing the target process.
    /// </value>
    /// <remarks>
    /// This property holds the reference to the process the MemoryEditor is attached to. 
    /// Ensure this property is set to a valid process before performing memory operations.
    /// </remarks>
    public Process CurrentProcess { get; private set; }

    /// <summary>
    /// Represents memory information for a specific region of a process.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        /// <summary>
        /// The base address of the region.
        /// </summary>
        public IntPtr BaseAddress;

        /// <summary>
        /// The base address of the allocation.
        /// </summary>
        public IntPtr AllocationBase;

        /// <summary>
        /// The memory protection of the allocation.
        /// </summary>
        public uint AllocationProtect;

        /// <summary>
        /// The size of the region in bytes.
        /// </summary>
        public IntPtr RegionSize;

        /// <summary>
        /// The state of the pages in the region.
        /// </summary>
        public uint State;

        /// <summary>
        /// The access protection of the pages in the region.
        /// </summary>
        public uint Protect;

        /// <summary>
        /// The type of pages in the region.
        /// </summary>
        public uint Type;
    }

    /// <summary>
    /// Specifies the type of memory dump to generate.
    /// </summary>
    public enum MiniDumpType : uint
    {
        /// <summary>
        /// Includes all accessible memory in the dump file.
        /// </summary>
        WithFullMemory = 2
    }

    /// <summary>
    /// Specifies access rights for process operations.
    /// </summary>
    [Flags]
    public enum ProcessAccessFlags : uint
    {
        /// <summary>
        /// Grants all access rights to the process.
        /// </summary>
        All = 0x001F0FFF,

        /// <summary>
        /// Grants the right to terminate the process.
        /// </summary>
        Terminate = 0x00000001,

        /// <summary>
        /// Grants the right to create threads in the process.
        /// </summary>
        CreateThread = 0x00000002,

        /// <summary>
        /// Grants the right to perform virtual memory operations.
        /// </summary>
        VMOperation = 0x00000008,

        /// <summary>
        /// Grants the right to read from the process's virtual memory.
        /// </summary>
        VMRead = 0x00000010,

        /// <summary>
        /// Grants the right to write to the process's virtual memory.
        /// </summary>
        VMWrite = 0x00000020,

        /// <summary>
        /// Grants the right to duplicate handles.
        /// </summary>
        DupHandle = 0x00000040,

        /// <summary>
        /// Grants the right to set information about the process.
        /// </summary>
        SetInformation = 0x00000200,

        /// <summary>
        /// Grants the right to query information about the process.
        /// </summary>
        QueryInformation = 0x00000400,

        /// <summary>
        /// Grants the right to synchronize with the process.
        /// </summary>
        Synchronize = 0x00100000
    }

    /// <summary>
    /// Provides an interface to access memory values in a specific process.
    /// </summary>
    public class MemoryValueAccessor
    {
        private readonly IntPtr address;
        private readonly MemoryEditor memoryEditor;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryValueAccessor"/> class.
        /// </summary>
        /// <param name="editor">The memory editor managing the process.</param>
        /// <param name="addr">The memory address to access.</param>
        public MemoryValueAccessor(MemoryEditor editor, IntPtr addr)
        {
            memoryEditor = editor;
            address = addr;
        }

        /// <summary>
        /// Reads the value at the memory address as a specified type.
        /// </summary>
        /// <typeparam name="T">The type of the value to read.</typeparam>
        /// <returns>The value read from memory.</returns>
        public T As<T>()
        {
            return memoryEditor.Get<T>(address);
        }
    }

    /// <summary>
    /// Represents an entry in a cheat table.
    /// </summary>
    public class CheatEntry
    {
        /// <summary>
        /// The memory address of the cheat entry.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The description of the cheat entry.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The unique identifier for the cheat entry.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// A list of offsets used to resolve the pointer chain for this entry.
        /// </summary>
        public List<int> Offsets { get; set; } = new List<int>();

        /// <summary>
        /// Indicates whether the value should be displayed as signed.
        /// </summary>
        public bool ShowAsSigned { get; set; }

        /// <summary>
        /// The data type of the cheat entry.
        /// </summary>
        public string Type { get; set; }
    }

    /// <summary>
    /// Represents a process and its metadata.
    /// </summary>
    public class ProcessInfo
    {

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string representation of the process, including ID, name, and type.</returns>
        public override string ToString() => $"{Id:X8} - {Name} ({Type})";

        /// <summary>
        /// The ID of the process.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The name of the process.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type or category of the process.
        /// </summary>
        public string Type { get; set; }
    }

    #region DllImports

    [DllImport("kernel32.dll")]
    public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out uint lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("Dbghelp.dll")]
    public static extern bool MiniDumpWriteDump(IntPtr hProcess, uint ProcessId, SafeHandle hFile, MiniDumpType DumpType, IntPtr ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);
    #endregion


}

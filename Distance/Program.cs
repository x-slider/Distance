using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal class Program
{
     [DllImport("kernel32.dll")]
     public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

     [DllImport("kernel32.dll", SetLastError = true)]
     public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

     [DllImport("kernel32.dll", SetLastError = true)]
     public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

     const int PROCESS_WM_READ = 0x0010;
     const int PROCESS_VM_WRITE = 0x0020;
     const int PROCESS_VM_OPERATION = 0x0008;

     [Flags]
     public enum ProcessAccessFlags : uint
     {
         VirtualMemoryRead = 0x0010,
     }

    static void Main(string[] args)
    {
        var processId = GetProcessIdByName("dota2");
        if (processId == -1)
        {
            Console.WriteLine("Process dota2.exe not found.");
            return;
        }

        IntPtr processHandle = OpenProcess(PROCESS_WM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, processId);

        if (processHandle == IntPtr.Zero)
        {
            Console.WriteLine("Problem with process opening.");
            return;
        }

        var (baseAddress, moduleSize) = GetModuleBaseAndSize(processId, "client.dll");
        if (baseAddress == IntPtr.Zero)
        {
            Console.WriteLine("Module not found client.dll.");
            return;
        }

        Console.WriteLine($"Base address client.dll: 0x{baseAddress.ToString("X")}, ModuleSize: {moduleSize}\n");

        string patternStr = "00 00 ?? 44 00 00 8C 42";
        var (pattern, skipIndexes) = ParsePatternString(patternStr);
        byte[] buffer = new byte[moduleSize];
        int bytesRead;

        if (ReadProcessMemory(processHandle, baseAddress, buffer, buffer.Length, out bytesRead))
        {
            IntPtr startAddress = new IntPtr(0);
            var addresses = FindPatternAddresses(processHandle, baseAddress, buffer, bytesRead, pattern, skipIndexes, startAddress);

            IntPtr chosenAddress = IntPtr.Zero;

            if (addresses.Count > 1)
            {
                for (int i = 0; i < addresses.Count; i++)
                {
                    Console.WriteLine($"{i}: Address: 0x{addresses[i].ToString("X")}");
                }
                Console.Write("Choose an address index: ");
                if (int.TryParse(Console.ReadLine(), out int index) && index >= 0 && index < addresses.Count)
                {
                    chosenAddress = addresses[index];
                }
                else
                {
                    Console.WriteLine("Invalid selection.");
                    return;
                }
            }
            else if (addresses.Count == 1)
            {
                chosenAddress = addresses[0];
            }
            else
            {
                Console.WriteLine("Pattern not found.");
                return;
            }

            while (true)
            {
                byte[] readBuffer = new byte[4];
                if (ReadProcessMemory(processHandle, chosenAddress, readBuffer, readBuffer.Length, out bytesRead) && bytesRead == readBuffer.Length)
                {
                    float currentValue = BitConverter.ToSingle(readBuffer, 0);
                    Console.WriteLine($"Current value at 0x{chosenAddress.ToString("X")}: {currentValue}");

                    Console.Write("Enter new value (or 'q' to quit): ");
                    string input = Console.ReadLine();
                    if (input.ToLower() == "q") break;

                    if (float.TryParse(input, out float newValue))
                    {
                        WriteFloatValue(processHandle, chosenAddress, newValue);
                    }
                    else
                    {
                        Console.WriteLine("Invalid input.");
                    }
                }
                else
                {
                    Console.WriteLine("Read error. Ensure the game is running and try again.");
                    break;
                }
            }

        }
        else
        {
            Console.WriteLine("Failed to read process memory.");
        }
    }

    static int GetProcessIdByName(string processName)
    {
        foreach (var process in Process.GetProcesses())
        {
            if (process.ProcessName.StartsWith(processName, StringComparison.OrdinalIgnoreCase))
            {
                return process.Id;
            }
        }
        return -1;
    }

    static (IntPtr baseAddress, int moduleSize) GetModuleBaseAndSize(int processId, string moduleName)
    {
        var process = Process.GetProcessById(processId);
        foreach (ProcessModule module in process.Modules)
        {
            if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                return (module.BaseAddress, module.ModuleMemorySize);
            }
        }

        return (IntPtr.Zero, 0);
    }

    static List<IntPtr> FindPatternAddresses(IntPtr hProcess, IntPtr baseAddress, byte[] buffer, int bytesRead, List<byte?> pattern, List<int> skipIndexes, IntPtr startAddress)
    {
        List<IntPtr> addresses = new List<IntPtr>();
        long startOffset = startAddress.ToInt64() > baseAddress.ToInt64() ? startAddress.ToInt64() - baseAddress.ToInt64() : 0;

        for (int i = (int)startOffset; i <= bytesRead - pattern.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Count; j++)
            {
                if (skipIndexes.Contains(j) || pattern[j] == null || buffer[i + j] == pattern[j])
                {
                    continue;
                }
                match = false;
                break;
            }
            if (match)
            {
                addresses.Add(IntPtr.Add(baseAddress, i));
            }
        }

        return addresses;
    }

    static (List<byte?> pattern, List<int> skipIndexes) ParsePatternString(string patternStr)
    {
        var bytes = new List<byte?>();
        var skipIndexes = new List<int>();
        var parts = patternStr.Split(' ');

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "??")
            {
                bytes.Add(null);
                skipIndexes.Add(i);
            }
            else
            {
                bytes.Add(Convert.ToByte(parts[i], 16));
            }
        }

        return (bytes, skipIndexes);
    }
    static void WriteFloatValue(IntPtr processHandle, IntPtr address, float value)
    {
        byte[] bytesToWrite = BitConverter.GetBytes(value);
        if (WriteProcessMemory(processHandle, address, bytesToWrite, bytesToWrite.Length, out int bytesWritten) && bytesWritten == bytesToWrite.Length)
        {
            Console.WriteLine("Value successfully written.\n");
        }
        else
        {
            Console.WriteLine("Write error.");
        }
    }
}
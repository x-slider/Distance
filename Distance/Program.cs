using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal class Program
{
    const int PROCESS_WM_READ = 0x0010;
    const int PROCESS_VM_WRITE = 0x0020;
    const int PROCESS_VM_OPERATION = 0x0008;

    static IntPtr distanceAddress;

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

    private static IntPtr GetModuleBaseAddress(Process process, string moduleName)
    {
        foreach (ProcessModule module in process.Modules)
        {
            if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                return module.BaseAddress;
            }
        }
        return IntPtr.Zero;
    }

    private static void Main(string[] args)
    {
        Console.WriteLine("Getting PID for dota2.exe");
        Process process = Process.GetProcessesByName("dota2")[0];
        IntPtr processHandle = OpenProcess(PROCESS_WM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, process.Id);

        if (process != null)
        { 
            IntPtr clientDllBaseAddress = GetModuleBaseAddress(process, "client.dll");
            if (clientDllBaseAddress != IntPtr.Zero)
            {
                Console.WriteLine($"client.dll base address: {clientDllBaseAddress.ToString("X")}");
                int offset = 0x4641348;
                distanceAddress = IntPtr.Add(clientDllBaseAddress, offset);
                Console.WriteLine($"Distance address (client.dll + {offset.ToString("X")}): {distanceAddress.ToString("X")}");
            }
            else
            {
                Console.WriteLine("client.dll not found.");
                return; 
            }

            while (true) 
            {
                byte[] buffer = new byte[4];
                if (ReadProcessMemory(processHandle, distanceAddress, buffer, buffer.Length, out int bytesRead))
                {
                    float currentValue = BitConverter.ToSingle(buffer, 0);
                    Console.WriteLine($"Current value: {currentValue}");

                    Console.Write("\nEnter new value: ");
                    string input = Console.ReadLine();
                    if (float.TryParse(input, out float newValue))
                    {
                        byte[] bytesToWrite = BitConverter.GetBytes(newValue);
                        if (WriteProcessMemory(processHandle, distanceAddress, bytesToWrite, bytesToWrite.Length, out int bytesWritten))
                        {
                            Console.WriteLine("Value successfully written.");
                        }
                        else
                        {
                            Console.WriteLine("Write error.");
                        }
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
    }
}

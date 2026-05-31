using System.Diagnostics;
using System.Net.Sockets;

namespace ProxyMapService.Vite
{
    public static class ViteLauncher
    {
        public static void StartIfNeeded()
        {
            const int vitePort = 5173;

            if (IsPortOpen("localhost", vitePort))
                return;

            var dashboardDir = Path.GetFullPath(@"..\dashboard");

            var psi = new ProcessStartInfo
            {
                FileName = "npm",
                Arguments = "run dev",
                WorkingDirectory = dashboardDir,
                UseShellExecute = true
            };

            Process.Start(psi);
        }

        private static bool IsPortOpen(string host, int port)
        {
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(host, port);

                return task.Wait(500);
            }
            catch
            {
                return false;
            }
        }
    }
}

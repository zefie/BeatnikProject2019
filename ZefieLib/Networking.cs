using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace ZefieLib
{
    public class Networking
    {
        /// <summary>
        /// Checks if the specified port is in use
        /// </summary>
        /// <param name="port"></param>
        /// <param name="address">IPAdddress, defaults to localhost</param>
        /// <returns>True if the port is avaible, false if it is in use</returns>
        public static bool IsPortAvailable(int port, IPAddress address = null)
        {
            // 127.0.0.1
            if (address == null)
                address = Dns.GetHostEntry("localhost").AddressList[0];

            try
            {
                TcpListener tcpListener = new TcpListener(address, port);
                tcpListener.Start();
                tcpListener.Stop();
                return true;
            }
            catch { }
            return false;
        }
    }
}

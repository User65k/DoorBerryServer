using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace DoorKeeper
{
    public class Notifier {
        //Notify n clients about the Bell
        private List<Socket> clients = new List<Socket>();
        private Socket listener;

        internal Notifier() {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 11000);

            // Create a TCP/IP socket.
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );

            // Bind the socket to the local endpoint and listen for incoming connections.
            try {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                listener.BeginAccept( 
                    new AsyncCallback(AcceptCallback),
                    listener );

			} catch (SocketException se) {
				Console.WriteLine("SocketException : {0}",se.ToString());
			} catch (Exception e) {
				Console.WriteLine("Unexpected exception : {0}", e.ToString());
            }

        }

        void AcceptCallback(IAsyncResult ar) {
            Socket server = (Socket)ar.AsyncState;
            Socket client = server.EndAccept(ar);
            server.BeginAccept(AcceptCallback, server);
            // client socket logic...

            clients.Add(client);
        }

        internal void notifyall(){
            foreach(Socket clientSocket in clients)
            {
                try {
                    clientSocket.Send(Encoding.ASCII.GetBytes("\r\n"));
                } catch (SocketException se) {
                    Console.WriteLine("SocketException : {0}",se.ToString());
                } catch (Exception e) {
                    Console.WriteLine("Unexpected exception : {0}", e.ToString());
                }
            }
        }

		~Notifier ()
		{
            foreach(Socket clientSocket in clients)
            {
			    clientSocket.Close ();
            }
            listener.Close ();
		}
    }
}
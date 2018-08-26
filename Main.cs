using System;
using Raspberry.IO.GeneralPurpose;
using System.Diagnostics;
using System.Threading;

using System.Net;
using System.Net.Sockets;

namespace DoorKeeper
{
	class DoorBell
	{
		public static void Main (string[] args)
		{			
			new DoorBell();
			
			do {
				Thread.Sleep(10000);
			}while(true);
		}
		
		private Stopwatch debouncing;
		private GpioConnection door_ctrl, bell_conn, open_conn;
		private Asterisk asteriskManager;
		private Notifier bellManager;
		//OpenDoor + Activate Voice
		//private const ConnectorPin Buzzer = ConnectorPin.P1Pin36, TelAn = ConnectorPin.P1Pin22;
		private const ConnectorPin Buzzer = ConnectorPin.P1Pin35, TelAn = ConnectorPin.P1Pin33;
		
		bool selfTest()
		{
			door_ctrl[Buzzer] = true;
			Thread.Sleep(50);
			if(door_ctrl[Buzzer] != true) {
				Console.WriteLine ("Buzzer Hi not working");
				return false;
			}
			door_ctrl[Buzzer] = false;
			Thread.Sleep(50);
			if(door_ctrl[Buzzer] != false) {
				Console.WriteLine ("Buzzer Lo not working");
				return false;
			}
			
			door_ctrl[TelAn] = true;
			Thread.Sleep(50);
			if(door_ctrl[TelAn] != true) {
				Console.WriteLine ("TelAn Hi not working");
				return false;
			}
			door_ctrl[TelAn] = false;
			Thread.Sleep(50);
			if(door_ctrl[TelAn] != false) {
				Console.WriteLine ("TelAn Lo not working");
				return false;
			}
			return true;
		}
		
		DoorBell()
		{
			//detect bell
			//bell_conn  =  new GpioConnection(ConnectorPin.P1Pin13.Input().PullDown());
			bell_conn  =  new GpioConnection(ConnectorPin.P1Pin29.Input().PullDown());
			
			//detect if opened
			//open_conn  =  new GpioConnection(ConnectorPin.P1Pin40.Input().PullDown());
			open_conn  =  new GpioConnection(ConnectorPin.P1Pin31.Input().PullDown());
						
			door_ctrl = new GpioConnection(new PinConfiguration[]
                           {
                               Buzzer.Output().Disable(),
                               TelAn.Output().Disable()
						   });
			
			//self test
			//selfTest();

			asteriskManager = new Asterisk();
			
			asteriskManager.DTMFCallback += DTMF_Callback;
			asteriskManager.StateCallback += SIP_Event;
			bell_conn.PinStatusChanged += BellIsPushed;
			open_conn.PinStatusChanged += DoorGetsOpened;
			
			CLIInterface cli = new CLIInterface();
			cli.TuerAuf += TuerAuf;

			bellManager = new Notifier();
			
			Console.WriteLine (DateTime.Now.ToString("dd.MM.yy HH:mm:ss")+" Door started");
		}
		~DoorBell()
		{
			open_conn.Close();
			bell_conn.Close();
			door_ctrl.Close();
		}
		
		void TuerAuf(object sender, int duration)
		{
			//open door
			door_ctrl[Buzzer] = true;
			Thread.Sleep(duration);
			door_ctrl[Buzzer] = false;
		}
		
		void BellIsPushed(object sender, PinStatusEventArgs eventArgs)
		{	
			//don't overreact and avoid false alarms by some wait time
			if(eventArgs.Enabled){
				debouncing = new Stopwatch();
				debouncing.Start();
			}else{
				debouncing.Stop();
				if(debouncing.Elapsed.TotalMilliseconds > 100) {//50 ist ein kurzer druck
					asteriskManager.call();
					bellManager.notifyall();
				}
				Console.WriteLine(DateTime.Now.ToString("dd.MM.yy HH:mm:ss")+" Bell 4 "+debouncing.Elapsed.TotalMilliseconds);
			}
		}
		void DoorGetsOpened(object sender, PinStatusEventArgs eventArgs)
		{
			if(eventArgs.Enabled)
			{
				//Buzzer triggered - stop notification call
				Console.WriteLine(DateTime.Now.ToString("dd.MM.yy HH:mm:ss")+" Door opened");
				asteriskManager.hangup();
			}
		}
		
		void DTMF_Callback(object sender, string DTMF)
        {
			if("5".Equals(DTMF))
			{
				//open door
				TuerAuf(sender, 1500);
			}
			Console.WriteLine (DateTime.Now.ToString("dd.MM.yy HH:mm:ss")+" DTMF "+DTMF);
        }
        void SIP_Event(object sender, string LineStatus)
        {
			if("Up".Equals(LineStatus))
			{
				//door telephone on
				door_ctrl[TelAn] = true;
			}
			if("Down".Equals(LineStatus))
			{
				//door telephone off
				door_ctrl[TelAn] = false;
			}
			Console.WriteLine (DateTime.Now.ToString("dd.MM.yy HH:mm:ss")+" SIP Status "+LineStatus);
        }

	}
	
	public class CLIInterface
	{
		//open the door as reaction to traffic on a port (remote control)
		private Socket listen_socket;
        internal event EventHandler<int> TuerAuf;

		public CLIInterface ()
		{
			EndPoint ep = new IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"),14000);//System.Net.IPAddress.Parse("127.0.0.1");

			listen_socket = new Socket (ep.AddressFamily, SocketType.Stream, ProtocolType.IP);
			try {
				listen_socket.Bind (ep);
				listen_socket.Listen(10);
			} catch (Exception e) {
				Console.WriteLine("Cannot listen: {0}", e.Message);
			}
			Thread thread1 = new Thread(new ThreadStart(A));
			thread1.Start();
		}
		private void A()
		{
			//try
			{
				while (true)
				{
					// Accepts new connections and sends some dummy byte array, then closes the socket.
					Socket acceptedSocket = listen_socket.Accept();
										
					//make a byte array and receive data from the client 
					Byte[] bReceive = new Byte[5];
					int i = acceptedSocket.Receive(bReceive);
			 
					if(i >= 3
					 && bReceive[0]=='A'
					 && bReceive[1]=='U'
					 && bReceive[2]=='F')
					{
						TuerAuf(this, 1500);
						Console.WriteLine(DateTime.Now.ToString("dd.MM.yy HH:mm:ss")+" Web Open");
					}
					
					acceptedSocket.Close(50);
				}
			}/*
			catch (Exception ex)
			{
				Console.WriteLine("Cannot rec: {0}", ex.Message);
			}*/
		}
		
		~CLIInterface ()
		{
			listen_socket.Close ();
		}
	}
}

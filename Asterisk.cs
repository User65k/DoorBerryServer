using System;
using System.Net;
using System.Net.Sockets;
//using System.Text.RegularExpressions;
using System.Text;

namespace DoorKeeper
{
	public class Asterisk
	{
		//use an asterisk manager to call/hangup on a number and listen to DTMF presses
		private const String channel_to_call = "SIP/**610#611#612#622@621";//"SIP/**610@621";
		private const String asterisk_pw = "password";
		private const String asterisk_usr = "username";

		private Socket clientSocket;
		private String curr_channel = null;
		
        internal event EventHandler<string> DTMFCallback;
        internal event EventHandler<string> StateCallback;

		/** test client * /
		public static void Main (string[] args)
		{
			Console.WriteLine ("Hello!");

			Asterisk asteriskManager = new Asterisk();
			
			int a = 0;
			do
			{
				a = Console.Read();
				
				switch (a)
				{
					case 'c':
						Console.WriteLine("alling...");
						Console.WriteLine("~~~~~~~~~~");
						asteriskManager.call();
					break;
					case 'h':
						Console.WriteLine("anging up...");
						Console.WriteLine("~~~~~~~~~~~~~");
						asteriskManager.hangup();
					break;
					case 'd':
						Console.WriteLine("isconnecting...");
						Console.WriteLine("~~~~~~~~~~~~~~~~");
					break;
					default:
						Console.WriteLine(" is no known command!");
					break;
				}
				
			}while(a!='d');
			
			asteriskManager.disconnect();
		}//*/
		
		
		internal Asterisk ()
		{
			if(!connect())
			{
				Console.WriteLine("Au weiha!");
			}
		}
		~Asterisk ()
		{
			if(clientSocket!=null && clientSocket.Connected)
				disconnect();
		}

		internal void call()
		{
			try {
				if(clientSocket==null || !clientSocket.Connected)
					connect();
				
				clientSocket.Send(Encoding.ASCII.GetBytes("Action: Originate\r\n" +
					"Channel: "+channel_to_call+"\r\n" +
					"Context: aussen\r\n" +
					"Exten: 621\r\n" +
					"Priority: 1\r\n" +
					"Callerid: door\r\n" +
					"Timeout: 30000\r\n" +
					"Async: yes\r\n\r\n"));

			} catch (SocketException se) {
				Console.WriteLine("SocketException : {0}",se.ToString());
				//TODO endlosschleife verhindern
				//disconnect();
				//if(connect())
				//{
				//	call();
				//}
			} catch (Exception e) {
				Console.WriteLine("Unexpected exception : {0}", e.ToString());
			}
			/*
Response: Success
Message: Originate successfully queued

Event: Newchannel
Privilege: call,all
Channel: SIP/621-00000004
ChannelState: 0
ChannelStateDesc: Down
CallerIDNum:
CallerIDName:
AccountCode:
Exten:
Context: aussen
Uniqueid: 1444739347.6

# #More Stuff


# #If picked up:

Event: Newstate
Privilege: call,all
Channel: SIP/621-00000004
ChannelState: 6
ChannelStateDesc: Up
CallerIDNum:
CallerIDName: door
ConnectedLineNum:
ConnectedLineName: door
Uniqueid: 1444739347.6

			*/
		}
		internal void hangup()
		{
			if(curr_channel == null) return;

			try {
				clientSocket.Send(Encoding.ASCII.GetBytes("ACTION: Hangup\r\n" +
					"Channel: "+curr_channel+"\r\n\r\n"));
			} catch (SocketException se) {
				Console.WriteLine("SocketException : {0}",se.ToString());
			} catch (Exception e) {
				Console.WriteLine("Unexpected exception : {0}", e.ToString());
			}
			curr_channel = null;
		}
		private void disconnect()
		{
			if(clientSocket==null || !clientSocket.Connected) return;
			try {
				clientSocket.Send(Encoding.ASCII.GetBytes("ACTION: LOGOFF\r\n\r\n"));

				clientSocket.Shutdown(SocketShutdown.Both);
				clientSocket.Close();
			} catch (SocketException se) {
				Console.WriteLine("SocketException : {0}",se.ToString());
			} catch (Exception e) {
				Console.WriteLine("Unexpected exception : {0}", e.ToString());
			}
		}

		private bool connect()
		{
			try {
				// Connect to the asterisk server.
				clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5038);
				clientSocket.Connect(serverEndPoint);

				byte[] buffer = new byte[30];
				int bytesRead = clientSocket.Receive(buffer);//skip Header
				
				// Login to the server; manager.conf needs to be setup with matching credentials.
				clientSocket.Send(Encoding.ASCII.GetBytes("Action: Login\r\n" +
					"Username: "+asterisk_usr+"\r\n" +
					"Secret: "+asterisk_pw+"\r\n\r\n"));

				bytesRead = clientSocket.Receive(buffer);
				string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);

				if (response.StartsWith("Response: Success\r\n"))
				{
				/*
Response: Success
Message: Authentication accepted

Event: FullyBooted
Privilege: system,all
Status: Fully Booted

				*/

					//start async listening
					messageBuffer = new byte[2];
					message = string.Empty;
					clientSocket.BeginReceive(messageBuffer, 0, 2, SocketFlags.None,
					new AsyncCallback(OnReceiveMessage), 1);

					return true;
				}else{
					clientSocket.Shutdown(SocketShutdown.Both);
					clientSocket.Close();

					return false;
				}

			} catch (SocketException se) {
				Console.WriteLine("SocketException : {0}",se.ToString());
			} catch (Exception e) {
				Console.WriteLine("Unexpected exception : {0}", e.ToString());
			}
			return false;
		}

		private void ParseResponse(string response)//resp has no line endings
		{
			//Console.WriteLine("< "+response);
										
			if(response.StartsWith("Event: "))//StateMachine: neuer event block
			{
				cur_event = response.Substring(7);
				//Console.WriteLine("Now in "+cur_event);
			}else if (string.IsNullOrEmpty(response))//empty line -> StateMachine: event block done
			{
				cur_event = "";
			}else if("DTMF".Equals(cur_event) && response.StartsWith("Digit: "))//StateMachine: Digit within DTMF Block
			{	
				if(DTMFCallback != null)
					DTMFCallback(this, response.Substring(7));
				
			}else if("Newchannel".Equals(cur_event) && response.StartsWith("Channel: "))//StateMachine: Channel within Newchannel Block
			{
				curr_channel = response.Substring(9);
				//Console.WriteLine("New Chan "+curr_channel);
			}else if("Newstate".Equals(cur_event) && curr_channel!=null)
			{
				if(response.StartsWith("Channel: "))
				{
					msg_channel = response.Substring(9);
					//Console.WriteLine("Newstate of chan "+msg_channel);
				}
				if(curr_channel.Equals(msg_channel) && response.StartsWith("ChannelStateDesc: "))
				{
					if(StateCallback != null)
						StateCallback(this, response.Substring(18));
					//Up / Ring / Down
				}
			}else if("Hangup".Equals(cur_event) && curr_channel!=null && response.Equals("Channel: "+curr_channel))
			{
				if(StateCallback != null)
					StateCallback(this, "Down");
			}
			
		}
		
	   private void OnReceiveMessage(IAsyncResult result)
		{
			try {
				int bytesRead = clientSocket.EndReceive(result);
				
				if(bytesRead==0)
				{
					disconnect();
					connect();
					return;
				}
				
				if (messageBuffer[0] == 13 && messageBuffer[1] == 10)
				{
					//clientSocket.Send(ASCIIEncoding.ASCII.GetBytes(connectionInfo.message), SocketFlags.None);
					//clientSocket.Send(connectionInfo.messageBuffer, SocketFlags.None);
					
					ParseResponse(message);
					
					message = string.Empty;
				}else if (messageBuffer[1] == 13)
				{
					message += ASCIIEncoding.ASCII.GetString(messageBuffer, 0, 1);
					
					byte[] buffer = new byte[1];
					clientSocket.Receive(buffer);
					if(buffer[0]==10)
					{
						ParseResponse(message);
					
						message = string.Empty;
					}else{
						// \r is dropped - but what ever
						message += ASCIIEncoding.ASCII.GetString(buffer, 0, 1);
					}
				}else{
					if (string.IsNullOrEmpty(message))
					{
						message = ASCIIEncoding.ASCII.GetString(messageBuffer);
					}
					else
					{
						message += ASCIIEncoding.ASCII.GetString(messageBuffer);
					}
				}
				
				clientSocket.BeginReceive(messageBuffer, 0, 2, SocketFlags.None,
					new AsyncCallback(OnReceiveMessage), 1);
				
			} catch (SocketException se) {
				Console.WriteLine("SocketException : {0}",se.ToString());
			} catch (Exception e) {
				Console.WriteLine("Unexpected exception : {0}", e.ToString());
			}
		}

	
		private Byte[] messageBuffer { get; set; }
		private string message { get; set; }
		private string cur_event = null;//whitch event block we are working on
		private string msg_channel = null;
	}
}


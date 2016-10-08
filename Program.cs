using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MonoScardOsX
{
	class MainClass
	{
		const int SCARD_SCOPE_USER = 0;
		const int SCARD_SCOPE_TERMINAL = 1;
		const int SCARD_SCOPE_SYSTEM = 2;

		const string SCARD_ALL_READERS = "SCard$AllReaders\000";
		const string SCARD_DEFAULT_READERS = "SCard$DefaultReaders\000";
		const string CARD_LOCAL_READERS = "SCard$LocalReaders\000";
		const string SCARD_SYSTEM_READERS = "SCard$SystemReaders\000";

		const int SCARD_SHARE_SHARED = 0x00000002; // - This application will allow others to share the reader
		const int SCARD_SHARE_EXCLUSIVE = 0x00000001; // - This application will NOT allow others to share the reader
		const int SCARD_SHARE_DIRECT = 0x00000003; // - Direct control of the reader, even without a card

		const int SCARD_PROTOCOL_T0 = 0x00000001;
		const int SCARD_PROTOCOL_T1 = 0x00000002;
		const int SCARD_PROTOCOL_RAW = 0x00000004;

		const int SCARD_LEAVE_CARD = 0;
		const int SCARD_RESET_CARD = 1;
		const int SCARD_UNPOWER_CARD = 2;
		const int SCARD_EJECT_CARD = 3;

		[DllImport("/System/Library/Frameworks/PCSC.framework/PCSC")]
		static extern uint SCardEstablishContext(int dwScope, IntPtr pvReserved1, IntPtr pvReserved2, out IntPtr phContext);

		[DllImport("/System/Library/Frameworks/PCSC.framework/PCSC", EntryPoint = "SCardListReaders", CharSet = CharSet.Ansi)]
		public static extern uint SCardListReaders(IntPtr hContext, string mszGroups, byte[] mszReaders, ref int pcchReaders);

		[DllImport("/System/Library/Frameworks/PCSC.framework/PCSC", EntryPoint = "SCardConnect", CharSet = CharSet.Auto)]
		static extern uint SCardConnect(
			IntPtr hContext,
			[MarshalAs(UnmanagedType.LPTStr)] string szReader,
			UInt32 dwShareMode,
			UInt32 dwPreferredProtocols,
			out IntPtr phCard,
			out UInt32 pdwActiveProtocol);


		[DllImport("/System/Library/Frameworks/PCSC.framework/PCSC")]
		static extern uint SCardStatus(
			IntPtr hCard,
			byte[] szReaderName,
			ref int pcchReaderLen,
			ref int pdwState,
			ref int pdwProtocol,
			byte[] pbAtr,
			ref int pcbAtrLen);

		[DllImport("/System/Library/Frameworks/PCSC.framework/PCSC")]
		static extern uint SCardTransmit(IntPtr hCard, SCARD_IO_REQUEST pioSendPci, byte[] pbSendBuffer, int cbSendLength, SCARD_IO_REQUEST pioRecvPci,
			byte[] pbRecvBuffer, ref int pcbRecvLength);


		[DllImport("/System/Library/Frameworks/PCSC.framework/PCSC")]
		static extern uint SCardDisconnect(IntPtr hCard, int dwDisposition);

		[DllImport("/System/Library/Frameworks/PCSC.framework/PCSC")]
		static extern uint SCardFreeMemory(IntPtr hContext, IntPtr pvMem);

		[DllImport("/System/Library/Frameworks/PCSC.framework/PCSC")]
		static extern uint SCardReleaseContext(IntPtr hContext);

		[StructLayout(LayoutKind.Sequential)]
		internal class SCARD_IO_REQUEST
		{
			internal uint dwProtocol;
			internal uint cbPciLength;
			public SCARD_IO_REQUEST(uint protocol)
			{
				dwProtocol = protocol;
				cbPciLength = (uint)Marshal.SizeOf(typeof(SCARD_IO_REQUEST));
			}
		}

		public static void dump(byte[] data, int length)
		{
			for (int i = 0; i < length; i++) {
				if((i % 16) == 0){Console.Write("\n");} 
				Console.Write(data[i].ToString("X02") + " ");
			}
		}


		public static void Main (string[] args)
		{
			IntPtr hContext;
			uint res = 0;


			res = SCardEstablishContext(SCARD_SCOPE_USER, IntPtr.Zero, IntPtr.Zero, out hContext);

			//Detect readers connected to the system
			byte[] bufReaderName = new byte[1024];
			int lenReaderName = bufReaderName.Length;
			res = SCardListReaders(hContext, SCARD_ALL_READERS, bufReaderName, ref lenReaderName);

			//Parse reader names
			List<string> readers = new List<string>();
			while (bufReaderName.Length > 0) {
				string s = System.Text.Encoding.ASCII.GetString (bufReaderName);
				if (s.Length == 0) { break; }
				readers.Add (s);
				bufReaderName = bufReaderName.Skip (s.Length + 1).ToArray();
			}
			Console.WriteLine ( readers.Count.ToString() + " reader(s) detected");
			foreach (var s in readers) { Console.WriteLine (" - "+s); }
			Console.WriteLine (""); 
				
			//Connect to the card
			IntPtr hCard;
			uint activeProtocol;
			res = SCardConnect (hContext, readers [0], SCARD_SHARE_SHARED, SCARD_PROTOCOL_T0 | SCARD_PROTOCOL_T1, 
				out hCard, out activeProtocol);
			
			//Command for selecting(activating) VISA card application
			byte[] sendBuffer = new byte[]{ 0x00, 0xa4, 0x04, 0x00, 0x05, 0xa0, 0x00,0x00,0x00,0x03,0x00 };

			//Command for selecting(activating) MasterCard card application
			//byte[] sendBuffer = new byte[]{ 0x00, 0xa4, 0x04, 0x00, 0x05, 0xa0, 0x00,0x00,0x00,0x04,0x00 };


			Console.Write("Card command:");
			dump (sendBuffer, sendBuffer.Length);
			Console.WriteLine ("");

			byte[] recvBuffer = new byte[1024];
			int lenRecv = recvBuffer.Length;

			SCARD_IO_REQUEST pci = new SCARD_IO_REQUEST (activeProtocol);
			res = SCardTransmit (hCard, pci, sendBuffer, sendBuffer.Length, pci, recvBuffer, ref lenRecv); 

			Console.Write("Card response:");
			dump (recvBuffer, lenRecv);

			res = SCardDisconnect (hCard, SCARD_UNPOWER_CARD);

			res = SCardReleaseContext (hContext);

		}
	}
}

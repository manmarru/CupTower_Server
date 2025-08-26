using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server;

enum DATATYPE
{
    DATATYPE_CHAT,
    DATATYPE_GAME,
    DATATYPE_TURN,
    DATATYPE_ENDGAME,
    DATATYPE_GAMESET,
    DATATYPE_USERINFO
}

struct PACKET
{
    public DATATYPE Type;
    public int DataSize;
    public byte[] Data;
}

class Server
{
    public const int MAXUSER = 3;
    public const int HEADERSIZE_DEFAULT = 8;
    public const int DATASIZE_GAMEACT = 8;
    public const int DATASIZE_NODATA = 0;

    Socket m_Server;
    Socket[] m_Clients = new Socket[MAXUSER];

    private object[] UserLock = new object[MAXUSER];
    private int m_CurUser = 0;
    private int m_Turn = 0;

    public void Initialize()
    {
        m_Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        m_Server.Bind(new IPEndPoint(IPAddress.Any, 25565));
        m_Server.Listen(10);

        for (m_CurUser = 0; m_CurUser < MAXUSER; ++m_CurUser)
        {
            m_Clients[m_CurUser] = m_Server.Accept();
            m_Clients[m_CurUser].Send(BitConverter.GetBytes(m_CurUser), 4, SocketFlags.None);
            System.Console.WriteLine($"User {m_CurUser} Connected");
        }

        for (int i = 0; i < MAXUSER; ++i)
        {
            UserLock[i] = new object();
            m_Clients[i].Send(BitConverter.GetBytes(999), 4, SocketFlags.None);
        }
    }

    public void Recv_N_Send(int UserNum)
    {
        PACKET SendPacket = new PACKET();
        byte[] RecvBuffer = new byte[512];
        byte[] Send = new byte[512];
        while (true)
        {
            if (false == Recv(4, UserNum, RecvBuffer))
                return;
            SendPacket.Type = (DATATYPE)BinaryPrimitives.ReadInt32BigEndian(RecvBuffer);
            System.Console.WriteLine("===========================");
            System.Console.WriteLine(SendPacket.Type);


            if (false == Recv(4, UserNum, RecvBuffer))
                return;
            SendPacket.DataSize = BinaryPrimitives.ReadInt32BigEndian(RecvBuffer);
            System.Console.WriteLine($"DataSize : {SendPacket.DataSize}");

            if (false == Recv(SendPacket.DataSize, UserNum, RecvBuffer))
                return;


            switch (SendPacket.Type)
            {
                case DATATYPE.DATATYPE_CHAT:
                    {
                        string message = Encoding.UTF8.GetString(RecvBuffer, 0, SendPacket.DataSize);
                        System.Console.WriteLine(message);

                        SendPacket.Data = Encoding.UTF8.GetBytes(message);
                        BroadCasting(SendPacket);
                        break;
                    }
                case DATATYPE.DATATYPE_GAME:
                    {
                        int CupPos = BinaryPrimitives.ReadInt32BigEndian(RecvBuffer);
                        int GameAct = BinaryPrimitives.ReadInt32BigEndian(RecvBuffer.AsSpan(4, 4));
                        System.Console.WriteLine($"CupPos : {CupPos}\nGameAct : {GameAct}");
                        SendPacket.Data = new byte[SendPacket.DataSize];
                        BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data.AsSpan(0, 4), CupPos);
                        BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data.AsSpan(4, 4), GameAct);
                        BroadCasting(SendPacket);
                        NextTurn();
                        break;
                    }
                case DATATYPE.DATATYPE_TURN:
                    {
                        NextTurn();
                        break;
                    }
                case DATATYPE.DATATYPE_ENDGAME:
                    {
                        //나머지 유저들에게 종료 알림
                        break;
                    }
                default:
                    System.Console.WriteLine("데이터 이상!");
                    return;
            }
        }
    }

    private bool Recv(int DataSize, int UserNum, byte[] ReturnData)
    {
        int RecvLength = 0;
        while (RecvLength < DataSize)
        {
            int bytes = m_Clients[UserNum].Receive(ReturnData, RecvLength, DataSize - RecvLength, SocketFlags.None);
            if (0 == bytes)
                return false;
            RecvLength += bytes;
        }

        return true;
    }

    private bool SendMessage(int UserNum, PACKET packet)
    {
        byte[] Sendbuffer = new byte[HEADERSIZE_DEFAULT + packet.DataSize];
        BinaryPrimitives.WriteInt32BigEndian(Sendbuffer.AsSpan(0, 4), (int)packet.Type);
        BinaryPrimitives.WriteInt32BigEndian(Sendbuffer.AsSpan(4, 4), packet.DataSize);
        Buffer.BlockCopy(packet.Data, 0, Sendbuffer, HEADERSIZE_DEFAULT, packet.DataSize);

        lock (UserLock[UserNum])
        {
            int SendSize = 0;
            int TotalSize = HEADERSIZE_DEFAULT + packet.DataSize;
            while (SendSize < TotalSize)
            {
                int Send = m_Clients[UserNum].Send(Sendbuffer, SendSize, TotalSize - SendSize, SocketFlags.None);
                if (Send <= 0)
                {
                    System.Console.WriteLine("DisConnected While Calling SendMessage");
                    return false;
                }
                SendSize += Send;
            }
        }

        return true;
    }

    private void BroadCasting(PACKET Packet)
    {
        byte[] Sendbuffer = new byte[Packet.DataSize + HEADERSIZE_DEFAULT];
        BinaryPrimitives.WriteInt32BigEndian(Sendbuffer.AsSpan(0, 4), (int)Packet.Type);
        BinaryPrimitives.WriteInt32BigEndian(Sendbuffer.AsSpan(4, 4), Packet.DataSize);
        Buffer.BlockCopy(Packet.Data, 0, Sendbuffer, HEADERSIZE_DEFAULT, Packet.DataSize);

        int TotalSize;
        int SendSize;
        for (int i = 0; i < MAXUSER; ++i)
        {
            lock (UserLock[i])
            {
                SendSize = 0;
                TotalSize = HEADERSIZE_DEFAULT + Packet.DataSize;
                while (SendSize < TotalSize)
                {
                    int Send = m_Clients[i].Send(Sendbuffer, SendSize, TotalSize - SendSize, SocketFlags.None);
                    if (Send <= 0)
                    {
                        System.Console.WriteLine("DisConnected While Calling BroadCasting");
                        return;
                    }
                    SendSize += Send;
                }
            }
        }
    }

    public void Release()
    {
        System.Console.WriteLine("Release Start");
        for (int i = 0; i < MAXUSER; ++i)
        {
            m_Clients[i].Shutdown(SocketShutdown.Both);
            m_Clients[i].Close();
        }
        m_Server.Close();
    }

    private void NextTurn()
    {
        PACKET SendPacket;

        // 턴 넘기기
        m_Turn = (m_Turn + 1) % MAXUSER;
        System.Console.WriteLine($"Player {m_Turn} Turn");
        SendPacket.Type = DATATYPE.DATATYPE_TURN;
        SendPacket.DataSize = HEADERSIZE_DEFAULT + 4;
        SendPacket.Data = new byte[SendPacket.DataSize];
        BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data, m_Turn);
        BroadCasting(SendPacket);
    }

    private void Initialize_Table()
    {
        List<int> Table = new List<int>();
        List<int> User = new List<int>(MAXUSER);
        for (int i = 0; i < MAXUSER; ++i)
        {
            Table.Add(0);
            Table.Add(1);
            Table.Add(2);
        }

        Random rand = new Random();
        int Remain = 27;
        int Index;
        PACKET SendPacket = new PACKET();
        SendPacket.Data = new byte[12];
        for (int player = 0; player < MAXUSER; ++player)
        {
            int[] PlayerInfo = new int[9];
            for (int i = 0; i < 9; ++i)
            {
                Index = rand.Next(0, Remain); // [0, Remain - 1]
                ++PlayerInfo[Table[Index]];
                Table.RemoveAt(Index);
                --Remain;
            }

            SendPacket.Type = DATATYPE.DATATYPE_USERINFO;
            SendPacket.DataSize = 12;
            BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data.AsSpan(0, 4), PlayerInfo[0]);
            BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data.AsSpan(4, 4), PlayerInfo[1]);
            BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data.AsSpan(8, 4), PlayerInfo[2]);
            SendMessage(player, SendPacket);
        }
    }
    
    
}



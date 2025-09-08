using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server;

enum DATATYPE
{
    DATATYPE_DEBUG,
    DATATYPE_GAME,
    DATATYPE_TURN,
    DATATYPE_ENDGAME,
    DATATYPE_GAMESET,
    DATATYPE_USERINFO
}
enum DATA { DATA_UNACTABLE, DATA_SKIPTURN };

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
    public const int ROUNDSIZE = 2;

    Socket m_Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    Socket[] m_Clients = new Socket[MAXUSER];

    private object[] UserLock = new object[MAXUSER];
    private int m_CurUser = 0;
    private int m_Turn = 0;
    private int m_SkipCount = 1;
    private int m_Round = 0;
    private int[] m_UserScore = new int[MAXUSER];
    private int[] m_WinnerScore = new int[MAXUSER];

    public void Initialize()
    {
        m_Server.Bind(new IPEndPoint(IPAddress.Any, 25565));
        m_Server.Listen(10);

        byte[] Temp = new byte[4];
        for (m_CurUser = 0; m_CurUser < MAXUSER; ++m_CurUser)
        {
            m_Clients[m_CurUser] = m_Server.Accept();
            BinaryPrimitives.WriteInt32BigEndian(Temp, m_CurUser);
            m_Clients[m_CurUser].Send(Temp, 4, SocketFlags.None);
            System.Console.WriteLine($"User {m_CurUser} Connected");
        }

        System.Console.WriteLine("All Users Connected");
        BinaryPrimitives.WriteInt32BigEndian(Temp, 999);
        for (int i = 0; i < MAXUSER; ++i)
        {
            m_UserScore[i] = 0;
            m_WinnerScore[i] = 0;

            UserLock[i] = new object();
            m_Clients[i].Send(Temp, 4, SocketFlags.None);
        }

        Initialize_Table();
    }

    public void Recv_N_Send(int UserNum)
    {
        PACKET SendPacket = new PACKET();
        byte[] RecvBuffer = new byte[512];
        byte[] Send = new byte[512];
        while (true)
        {
            if (false == Recv(sizeof(int), UserNum, RecvBuffer))
                return;
            SendPacket.Type = (DATATYPE)BinaryPrimitives.ReadInt32BigEndian(RecvBuffer);
            System.Console.WriteLine("===========================");
            System.Console.WriteLine(SendPacket.Type);


            if (false == Recv(sizeof(int), UserNum, RecvBuffer))
                return;
            SendPacket.DataSize = BinaryPrimitives.ReadInt32BigEndian(RecvBuffer);
            System.Console.WriteLine($"DataSize : {SendPacket.DataSize}");

            if (false == Recv(SendPacket.DataSize, UserNum, RecvBuffer))
                return;


            switch (SendPacket.Type)
            {
                case DATATYPE.DATATYPE_DEBUG:
                    {
                        string message = Encoding.UTF8.GetString(RecvBuffer, 0, SendPacket.DataSize);
                        System.Console.WriteLine(message);

                        SendPacket.Data = Encoding.UTF8.GetBytes(message);
                        BroadCasting(SendPacket);
                        break;
                    }
                case DATATYPE.DATATYPE_GAME:
                    {
                        m_SkipCount = 0;
                        ++m_UserScore[UserNum];
                        int CupPos = BinaryPrimitives.ReadInt32BigEndian(RecvBuffer);
                        int GameAct = BinaryPrimitives.ReadInt32BigEndian(RecvBuffer.AsSpan(4, 4));
                        System.Console.WriteLine($"             CupPos : {CupPos}\nGameAct : {GameAct}");
                        SendPacket.Data = new byte[SendPacket.DataSize];
                        BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data.AsSpan(0, 4), CupPos);
                        BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data.AsSpan(4, 4), GameAct);
                        BroadCasting(SendPacket);
                        NextTurn();
                        break;
                    }
                case DATATYPE.DATATYPE_TURN:
                    {
                        int Msg = BinaryPrimitives.ReadInt32BigEndian(RecvBuffer);
                        if (Msg == (int)DATA.DATA_SKIPTURN) // 턴 스킵 or 타임아웃
                        {
                            m_SkipCount = 0;
                            if (m_Turn != UserNum)
                            {
                                System.Console.WriteLine("Skip Error : Turn");
                                continue;
                            }
                            System.Console.WriteLine("skip Button");
                            NextTurn();
                            break;
                        }

                        System.Console.WriteLine("          Unactable");
                        ++m_SkipCount;
                        System.Console.WriteLine($"SkipCount : {m_SkipCount}");
                        if (m_SkipCount == MAXUSER)
                        {
                            ++m_Round;
                            System.Console.WriteLine($"         Round{m_Round} GameSet!");
                            ++m_WinnerScore[RoundWinner()];
                            if (m_Round == ROUNDSIZE)
                            {
                                System.Console.WriteLine("          EndGame!");
                                SendPacket.Type = DATATYPE.DATATYPE_ENDGAME;
                                SendPacket.DataSize = sizeof(int);
                                BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data, FinalWinner());
                                BroadCasting(SendPacket);
                            }
                            else
                            {
                                SendPacket.Type = DATATYPE.DATATYPE_GAMESET;
                                SendPacket.DataSize = sizeof(int);
                                BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data, RoundWinner());
                                BroadCasting(SendPacket);

                                Initialize_Table();

                                m_Turn = MAXUSER - 1;
                            }
                            m_SkipCount = 0;
                        }
                        System.Console.WriteLine(m_Turn);
                        NextTurn();
                        break;
                    }
                case DATATYPE.DATATYPE_ENDGAME: // 받고 꺼야되는거 맞다.
                    {
                        System.Console.WriteLine($"         User{UserNum} Shutdown");
                        m_Clients[UserNum].Close();
                        return;
                    }
                default:
                    System.Console.WriteLine("DATATYPE EXCEPTION!");
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

        int SendSize = 0;
        int TotalSize = HEADERSIZE_DEFAULT + packet.DataSize;

        lock (UserLock[UserNum])
        {
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
        System.Console.WriteLine("Release Called");
        for (int i = 0; i < MAXUSER; ++i)
        {
            m_Clients[i].Close();
        }
        m_Server.Close();
    }

    private void NextTurn()
    {
        PACKET SendPacket;

        // 턴 넘기기
        m_Turn = (m_Turn + 1) % MAXUSER;
        System.Console.WriteLine($"Go to Player {m_Turn} Turn");
        SendPacket.Type = DATATYPE.DATATYPE_TURN;
        SendPacket.DataSize = 4;
        SendPacket.Data = new byte[SendPacket.DataSize];
        BinaryPrimitives.WriteInt32BigEndian(SendPacket.Data, m_Turn);
        BroadCasting(SendPacket);
    }

    private void Initialize_Table()
    {
        List<int> Table = new List<int>();
        for (int i = 0; i < 12; ++i)
        {
            Table.Add(0);
            Table.Add(1);
            Table.Add(2);
        }

        Random rand = new Random();
        int Remain = 36;
        int Index;
        PACKET SendPacket = new PACKET();
        SendPacket.Data = new byte[12];
        for (int player = 0; player < MAXUSER; ++player)
        {
            m_UserScore[player] = 0;

            int[] PlayerInfo = new int[3];
            for (int i = 0; i < 12; ++i)
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

    private int RoundWinner() // 동점이면 후턴이 이긴다
    {
        int winner = MAXUSER - 1;
        int HighScore = 0;
        for (int i = 0; i < MAXUSER; ++i)
        {
            if (m_UserScore[i] > HighScore)
            {
                winner = i;
                HighScore = m_UserScore[i];
            }
        }

        return winner;
    }

    private int FinalWinner()
    {
        int winner = MAXUSER - 1;
        int HighScore = 0;
        for (int i = 0; i < MAXUSER; ++i)
        {
            if (m_WinnerScore[i] > HighScore)
            {
                winner = i;
                HighScore = m_WinnerScore[i];
            }
        }

        return winner;
    }
}
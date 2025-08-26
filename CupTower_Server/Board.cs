
struct USER
{
    public USER() { }

    public int[] Remain = new int[3]{0, 0, 0};
}

class CBoard
{
    enum CUPTYPE { TYPE_BLANK = -1, TYPE_0 = 0, TYPE_1, TYPE_2};

    const int STACKSIZE = 27;
    private List<USER> Users;
    private List<CUPTYPE> m_StackCups;
    private readonly int m_MaxUser;
    private int m_ActiveCups = 7;

    public CBoard(int MaxUser)
    {
        m_MaxUser = MaxUser;
        Users = new List<USER>(m_MaxUser);
        m_StackCups = new List<CUPTYPE>(STACKSIZE);
    }

    void Initialize_GameTable()
    {
        m_StackCups = new List<CUPTYPE>(STACKSIZE);
        Users = new List<USER>(m_MaxUser);

        List<int> Cups = new List<int>();
        Random rand = new Random();
        for (int i = 0; i < 9; ++i)
        {
            Cups.Add((int)CUPTYPE.TYPE_0);
            Cups.Add((int)CUPTYPE.TYPE_1);
            Cups.Add((int)CUPTYPE.TYPE_2);
        }

        int Index;
        for (int player = 0; player < m_MaxUser; ++player)
        {
            for (int i = 1; i <= 9; ++i)
            {
                Index = rand.Next(Cups.Count);
                ++Users[player].Remain[Cups[Index]];
                Cups.RemoveAt(Index);
            }
        }
    }

    void PlayerAct(int PlayerNum, int CupType, int StackIndex)
    {
        --Users[PlayerNum].Remain[CupType];

        m_StackCups[StackIndex] = (CUPTYPE)CupType;

        int OriginalIndex = StackIndex;
        int Floor = CheckFloor(ref StackIndex);

        if (Floor == 7)
            return;
        if (StackIndex != 0) // 좌측체크
        {
            if (m_StackCups[OriginalIndex - 1] == CUPTYPE.TYPE_BLANK)
            {
                int NextIndex = OriginalIndex + (6 + Floor);
                ++m_ActiveCups;
            }
        }
        if (StackIndex != 6 - Floor) //우측체크
        {
            if (m_StackCups[OriginalIndex + 1] == CUPTYPE.TYPE_BLANK)
            {
                int NextIndex = OriginalIndex + (6 - Floor) + 1;
                ++m_ActiveCups;
            }
        }
    }

    private int CheckFloor(ref int Index) // 인덱스로 층 체크하기
    {
        if (Index == STACKSIZE - 1)
        {
            Index = 0;
            return 6;
        }
        for (int i = 7; i >= 1; --i)
        {
            if (Index < i)
            {
                return 7 - i;
            }
            Index -= i;
        }

        System.Console.WriteLine("FlorCkeck Error!");
        return -1;
    }
}

struct USER
{
    public USER() { }

    public int[] Remain = new int[3]{0, 0, 0};
}

class CBoard
{
    private List<USER> UserCups;
    private readonly int m_MaxUser;

    public CBoard(int MaxUser)
    {
        m_MaxUser = MaxUser;
        USER[] Users = new USER[MaxUser];
        List<int> Cups = new List<int>();

        Random rand = new Random();

        for (int i = 0; i < 9; ++i)
        {
            Cups.Add(0);
            Cups.Add(1);
            Cups.Add(2);
        }

        int Index;
        for (int player = 0; player < MaxUser; ++player)
        {
            for (int i = 1; i <= 9; ++i)
            {
                Index = rand.Next(Cups.Count);
                ++Users[player].Remain[Cups[Index]];
                Cups.RemoveAt(Index);
            }
        }
    }
}


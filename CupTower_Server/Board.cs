
struct USER
{
    public int Remain0;
    public int Remain1;
    public int Remain2;
}

class CBoard
{
    private List<USER> UserCups;
    private readonly int m_MaxUser;
    
    public CBoard(int MaxUser)
    {
        m_MaxUser = MaxUser;
        UserCups = new List<USER>();

        Random rand = new Random();

        int Remain0 = 9;
        int Remain1 = 9;
        int Remain2 = 9;

        for (int user = 0; user < MaxUser; ++user)
        {
            int Sum = 0;
            USER UserCup = new USER();
            UserCup.Remain0 = rand.Next(0, Math.Min(Remain0, 9 - Sum));
            Remain0 -= UserCup.Remain0;
            Sum += UserCup.Remain0;

            UserCup.Remain1 = rand.Next(0, Math.Min(Remain1, 9 - Sum));
            Remain1 -= UserCup.Remain1;
            Sum += UserCup.Remain1;

            UserCup.Remain2 = 9 - Sum;
            Remain2 -= Sum;

            UserCups.Add(UserCup);
        }
        


    }
}


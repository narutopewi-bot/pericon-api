namespace PericonAPI.Classes
{
    public class GameOrder
    {
        public Int16 User { get; set; }
        public Int16 Code { get; set; }
        public String Content { get; set; }

        public GameOrder() {
            User = -1; Code = -1; Content = String.Empty;
        }

        public GameOrder(int data) : this()
        {
            User = Convert.ToInt16(data);
        }

        public GameOrder(String data)
        {
            User = Convert.ToInt16(data.Substring(0, 4));
            Code = Convert.ToInt16(data.Substring(5, 4));
            Content = data.Substring(10, 20);
        }

        public static GameOrder FromString(String data)
        {
            GameOrder x = new GameOrder();
            x.User = Convert.ToInt16(data.Substring(0, 4));
            x.Code = Convert.ToInt16(data.Substring(5, 4));
            x.Content = data.Substring(10, 20);
            return x;
        }

        public override String ToString() { 
            return User.ToString("D4") + "-" + Code.ToString("D4") + "-" + Content.Substring(0,20); 
        }

    }
}

namespace PericonAPI.Classes
{
    public class GameUser
    {

        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int Level { get; set; }
        public DateTime RecDate { get; set; }
        public bool Active { get; set; }

        public GameUser()
        {
            Id = 0; Name = ""; Email = "nick@email.com"; Level = 0;
            RecDate = DateTime.Now; Active = true;
        }

        public GameUser(int id, string name, string email, int level) : this()
        {
            Id = id; Name = name; Email = email; Level = level;
        }
    }
}

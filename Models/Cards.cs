namespace PericonAPI.Models
{
    public class Card
    {
        public int Id { get; set; }
        public int Value { get; set; }
        public char Suit {  get; set; }
        public String Name { get; set; }
        public String Description { get; set; }
        public Boolean IsPlayed { get; set; }
        public int Point { get; set; }

        public Card() 
        {
            Id = -1; Name = String.Empty; Description = String.Empty; 
            IsPlayed = false; Value = 0; Point = 0;
        }
    }
    public class SpanishCards
    {
        public List<Card> Package = new List<Card>();
        private List<Card> OrderPack = new List<Card>();
        private int Cant = 40;

        public SpanishCards()
        {
            int s = 0;
            for(int x = 0; x < Cant; x++)  
            {
                Card w = new Card();
                w.Id = x;
                if (x < 10) 
                {
                    s = x + 1;
                    w.Suit = 'O';
                }
                if ((x > 9) && (x < 20)) 
                {
                    s = x - 9;
                    w.Suit = 'C';
                }
                if ((x > 19) && (x < 30)) 
                {
                    s = x - 19;
                    w.Suit = 'E';
                }
                if (x > 29) 
                {
                    s = x - 29;
                    w.Suit = 'B';
                }
                if (s > 7) s += 2;
                w.Value = s;
                w.Name = SetDescription(w.Value, w.Suit);
                w.IsPlayed = false;
                w.Description = "";
                w.Point = 0;
                OrderPack.Add(w);
            }
        }

        public void Reload()
        {
            RandomCards();
            Cant = Package.Count;
        }

        public int getCant() 
        { 
            return Package.Count; 
        }

        public void RandomCards()
        {
            Package.Clear();
            Package = new List<Card>(OrderPack);
            Cant = Package.Count;
            Random rng = new Random();
            for (int i = Package.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                Card temp = Package[i];
                Package[i] = Package[j];
                Package[j] = temp;
            }
        }

        public Card OutCard(int w)
        {
            if (Package.Count == 0) Reload();
            int index = Math.Clamp(w, 0, Package.Count - 1);
            Card v = Package[index];
            Package.RemoveAt(index);
            Cant = Package.Count;
            return v;
        }

        public Card OutCard()
        {
            if (Package.Count == 0) Reload();
            Card v = Package[0];
            Package.RemoveAt(0);
            Cant = Package.Count;
            return v;
        }

        public static int GetFaceValue(int cardId)
        {
            int num = cardId % 10;
            return num switch
            {
                0 => 1,
                1 => 2,
                2 => 3,
                3 => 4,
                4 => 5,
                5 => 6,
                6 => 7,
                7 => 10,
                8 => 11,
                9 => 12,
                _ => 0
            };
        }

        public static char GetSuit(int cardId)
        {
            int suitIndex = cardId / 10;
            return suitIndex switch
            {
                0 => 'O', // Oros
                1 => 'C', // Copas
                2 => 'E', // Espadas
                3 => 'B', // Bastos
                _ => ' '
            };
        }

        public String SetDescription(int a, char b) 
        {
            String k = String.Empty;
            switch(a) 
            {
                case 1:
                    k = "As";
                    break;
                case 2:
                    k = "Dos";
                    break;
                case 3:
                    k = "Tres";
                    break;
                case 4:
                    k = "Cuatro";
                    break;
                case 5:
                    k = "Cinco";
                    break;
                case 6:
                    k = "Seis";
                    break;
                case 7:
                    k = "Siete";
                    break;
                case 10:
                    k = "Sota";
                    break;
                case 11:
                    k = "Caballo";
                    break;
                case 12:
                    k = "Rey";
                    break;
            }
            switch (b) 
            {
                case 'O': 
                    k += " de Oros";
                    break;
                case 'C':
                    k += " de Copas";
                    break;
                case 'E':
                    k += " de Espadas";
                    break;
                case 'B':
                    k += " de Bastos";
                    break;
            }
            return k;
        }
    }
}

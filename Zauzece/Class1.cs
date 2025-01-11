using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Zauzece
{
    [Serializable]
    public class Class1
    {
        public int BrojParkinga { get; set; }
        public int BrojMjesta { get; set; }
        public int BrojSati { get; set; }

        public Class1(int brojParkinga, int brojMjesta, int brojSati)
        {
            BrojParkinga = brojParkinga;
            BrojMjesta = brojMjesta;
            BrojSati = brojSati;
        }

        public byte[] ToByteArray()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, this);
                return ms.ToArray();
            }
        }
        public static Class1 FromByteArray(byte[] byteArray)
        {
            using (MemoryStream ms = new MemoryStream(byteArray))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return (Class1)formatter.Deserialize(ms);
            }
        }

    }
}

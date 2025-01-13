using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Zauzece
{
    [Serializable]
    public class Class
    {
        public int BrojParkinga { get; set; }
        public int BrojMjesta { get; set; }
        public int BrojSati { get; set; }
        public string Proizvodjac { get; set; }
        public string Model { get; set; }
        public string Boja { get; set; }
        public string RegistarskiBroj { get; set; }
        public Class(int brojParkinga, int brojMjesta, int brojSati, string proizvodjac, string model, string boja, string registarskiBroj)
        {
            BrojParkinga = brojParkinga;
            BrojMjesta = brojMjesta;
            BrojSati = brojSati;
            Proizvodjac = proizvodjac;
            Model = model;
            Boja = boja;
            RegistarskiBroj = registarskiBroj;
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
        public static Class FromByteArray(byte[] byteArray)
        {
            using (MemoryStream ms = new MemoryStream(byteArray))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return (Class)formatter.Deserialize(ms);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

using Plus0_Bot.Resources.Datatypes;

using Plus0_Bot.Resources.Database;
using System.IO;
using System.Reflection;
using System.Xml;

//this file may be deleted, it was jsut part of the tutorial project I followed to learn the ins and outs of Disocrd.net

namespace Plus0_Bot.Core.Data
{
    public static class Data
    {
        public static int GetStones(ulong UserId)
        {
            using (var DBcontext = new SqliteDBContext())
            {
                if (DBcontext.Users.Where(x => x.UserId == UserId).Count() < 1)
                {
                    return 0;
                }
                return DBcontext.Users.Where(x => x.UserId == UserId).Select(x => x.Ammount).FirstOrDefault();
            }
        }

        public  static async Task SaveStones(ulong UserId, int Ammount)
        {
            using (var DBcontext = new SqliteDBContext())
            {
                if((DBcontext.Users.Where(x => x.UserId == UserId).Count() < 1))
                {
                    DBcontext.Users.Add(new User
                    {
                        UserId = UserId,
                        Ammount = Ammount

                    });
                }
                else
                {
                    User Current = DBcontext.Users.Where(x => x.UserId == UserId).FirstOrDefault();
                    Current.Ammount += Ammount;
                    DBcontext.Users.Update(Current);
                }
                await DBcontext.SaveChangesAsync();
                
            }
        }
        public static Sticker GetSticker()
        {
            string StickerLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location.Replace(@"bin\Debug\netcoreapp2.1", @"Data\Stickers.xml"));
            if(!File.Exists(StickerLocation))
            {
                return null;
            }

            //The file exist if you are here
            FileStream Stream = new FileStream(StickerLocation, FileMode.Open, FileAccess.Read);
            XmlDocument Doc = new XmlDocument();
            Doc.Load(Stream);
            Stream.Dispose();

            List<Sticker> Stickers = new List<Sticker>();

            foreach(XmlNode Node in Doc.DocumentElement)
            {
                Stickers.Add(new Sticker { name = Node.ChildNodes[0].InnerText, file = Node.ChildNodes[1].InnerText, description = Node.ChildNodes[2].InnerText});
            }
            Random Rand = new Random();
            int Number = Rand.Next(Stickers.Count);

            return Stickers[Number];
        }
    }
}

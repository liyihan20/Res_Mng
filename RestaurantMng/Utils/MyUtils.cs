using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Web;
using RestaurantMng.Models;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace RestaurantMng.Utils
{
    public class MyUtils
    {
        //生成随机数列
        public static string CreateValidateNumber(int length)
        {
            //去掉数字0和字母o,O，因为不容易区分
            string Vchar = "1,2,3,4,5,6,7,8,9,a,b,c,d,e,f,g,h,i,j,k,l,m,n,p" +
            ",q,r,s,t,u,v,w,x,y,z,A,B,C,D,E,F,G,H,I,J,K,L,M,N,P,Q" +
            ",R,S,T,U,V,W,X,Y,Z";


            string[] VcArray = Vchar.Split(new Char[] { ',' });//拆分成数组
            string num = "";

            int temp = -1;//记录上次随机数值，尽量避避免生产几个一样的随机数

            Random rand = new Random();
            //采用一个简单的算法以保证生成随机数的不同
            for (int i = 1; i < length + 1; i++)
            {
                if (temp != -1)
                {
                    rand = new Random(i * temp * unchecked((int)DateTime.Now.Ticks));
                }

                int t = rand.Next(58);
                if (temp != -1 && temp == t)
                {
                    return CreateValidateNumber(length);

                }
                temp = t;
                num += VcArray[t];
            }
            return num;
        }

        //生成验证码图片
        public static byte[] CreateValidateGraphic(string validateCode)
        {
            Bitmap image = new Bitmap((int)Math.Ceiling(validateCode.Length * 18.0), 32);
            Graphics g = Graphics.FromImage(image);
            try
            {
                //生成随机生成器
                Random random = new Random();
                //清空图片背景色
                g.Clear(Color.White);
                //画图片的干扰线
                for (int i = 0; i < 25; i++)
                {
                    int x1 = random.Next(image.Width);
                    int x2 = random.Next(image.Width);
                    int y1 = random.Next(image.Height);
                    int y2 = random.Next(image.Height);
                    g.DrawLine(new Pen(Color.Silver), x1, y1, x2, y2);
                }
                Font font = new Font("Arial", 14, (FontStyle.Bold | FontStyle.Italic));
                LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, image.Width, image.Height),
                 Color.Blue, Color.DarkRed, 1.2f, true);
                g.DrawString(validateCode, font, brush, 3, 2);
                //画图片的前景干扰点
                for (int i = 0; i < 100; i++)
                {
                    int x = random.Next(image.Width);
                    int y = random.Next(image.Height);
                    image.SetPixel(x, y, Color.FromArgb(random.Next()));
                }
                //画图片的边框线
                g.DrawRectangle(new Pen(Color.Silver), 0, 0, image.Width - 1, image.Height - 1);
                //保存图片数据
                MemoryStream stream = new MemoryStream();
                image.Save(stream, ImageFormat.Jpeg);
                //输出图片流
                return stream.ToArray();
            }
            finally
            {
                g.Dispose();
                image.Dispose();
            }
        }

        //验证复杂性密码
        public static string validatePassword(string str)
        {
            string alph = @"[A-Za-z]+";
            string num = @"\d+";
            string cha = @"[\-`=\\\[\];',\./~!@#\$%\^&\*\(\)_\+\|\{\}:""<>\?]+";
            if (!new Regex(alph).IsMatch(str))
            {
                return "新密码必须包含英文字母，保存失败。英文字母有：A~Z，a~z";
            }
            if (!new Regex(num).IsMatch(str))
            {
                return "新密码必须包含阿拉伯数字，保存失败。数字有：0~9";
            }
            if (!new Regex(cha).IsMatch(str))
            {
                return @"新密码必须包含特殊字符，保存失败。特殊字符有：-`=\[];',./~!@#$%^&*()_+|{}:""<>?";
            }
            return "";
        }

        public static string getMD5(string str)
        {
            //HashAlgorithm hash = HashAlgorithm.Create("MD5");
            //byte[] result = hash.ComputeHash(Encoding.Default.GetBytes(str));
            //return BitConverter.ToString(result);
            if (str.Length > 2)
            {
                str = "Who" + str.Substring(2) + "Are" + str.Substring(0, 2) + "You";
            }
            else
            {
                str = "Who" + str + "Are" + str + "You";
            }
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] data = Encoding.Default.GetBytes(str);
            byte[] result = md5.ComputeHash(data);
            String ret = "";
            for (int i = 0; i < result.Length; i++)
            {
                ret += result[i].ToString("x").PadLeft(2, '0');
            }
            return ret;

        }

        //将中文编码为utf-8
        public static string EncodeToUTF8(string str)
        {
            string result = System.Web.HttpUtility.UrlEncode(str, System.Text.Encoding.GetEncoding("UTF-8"));
            return result;
        }

        //将utf-8解码
        public static string DecodeToUTF8(string str)
        {
            string result = System.Web.HttpUtility.UrlDecode(str, System.Text.Encoding.GetEncoding("UTF-8"));
            return result;
        }

        public static bool hasGotPower(int userId, string controlerName, string actionName)
        {
            DiningMngEntities db = new DiningMngEntities();
            try
            {
                var power = from a in db.dn_authority
                            from u in db.dn_groups
                            from ga in a.dn_groupAuthority
                            from gu in u.dn_groupUser
                            where ga.group_id == u.id
                            && gu.user_id == userId
                            && a.controler_name == controlerName
                            && a.action_name == actionName
                            select a;
                if (power.Count() > 0)
                    return true;
                else
                    return false;
            }
            catch
            {
                return false;
            }

        }


        #region GetPicThumbnail
        /// <summary>
        /// 无损压缩图片
        /// </summary>
        /// <param name="sFile">原图片</param>
        /// <param name="dFile">压缩后保存位置</param>
        /// <param name="width">宽度尺寸</param>
        /// <param name="flag">压缩质量 1-100</param>
        /// <returns></returns>

        public static bool GetPicThumbnail(string sFile, string dFile, int width, int flag = 100)
        {
            Image iSource = Image.FromFile(sFile);
            ImageFormat tFormat = iSource.RawFormat;
            int sW = 0, sH = 0;

            //按比例缩放
            Size tem_size = new Size(iSource.Width, iSource.Height);

            sW = width < tem_size.Width ? width : tem_size.Width;
            sH = sW * tem_size.Height / tem_size.Width;

            Bitmap ob = new Bitmap(sW, sH);
            Graphics g = Graphics.FromImage(ob);
            g.Clear(Color.WhiteSmoke);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(iSource, new Rectangle(0, 0, sW, sH), 0, 0, iSource.Width, iSource.Height, GraphicsUnit.Pixel);
            g.Dispose();
            //以下代码为保存图片时，设置压缩质量
            EncoderParameters ep = new EncoderParameters();
            long[] qy = new long[1];
            qy[0] = flag;//设置压缩的比例1-100
            EncoderParameter eParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, qy);
            ep.Param[0] = eParam;
            try
            {
                ImageCodecInfo[] arrayICI = ImageCodecInfo.GetImageEncoders();
                ImageCodecInfo jpegICIinfo = null;
                for (int x = 0; x < arrayICI.Length; x++)
                {
                    if (arrayICI[x].FormatDescription.Equals("JPEG"))
                    {
                        jpegICIinfo = arrayICI[x];
                        break;
                    }
                }
                if (jpegICIinfo != null)
                {
                    ob.Save(dFile, jpegICIinfo, ep);//dFile是压缩后的新路径
                }
                else
                {
                    ob.Save(dFile, tFormat);
                }
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                iSource.Dispose();
                ob.Dispose();
            }
        }
        #endregion
    }
}
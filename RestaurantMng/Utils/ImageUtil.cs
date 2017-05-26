using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;

namespace RestaurantMng.Utils
{
    public class ImageUtil
    {

        //将保存在数据库的图片二进制转化为Image格式
        public static Image BytesToImage(byte[] buffer)
        {
            using (MemoryStream ms = new MemoryStream(buffer)) {
                Image image = System.Drawing.Image.FromStream(ms);
                return image;
            }
        }

        //生成缩略图
        public static byte[] MakeThumbnail(Image originalImage, int width=0, int height=128, string mode="H")
        {
            int towidth = width;
            int toheight = height;

            int x = 0;
            int y = 0;
            int ow = originalImage.Width;
            int oh = originalImage.Height;

            switch (mode) {
                case "HW"://指定高宽缩放（可能变形）                 
                    break;
                case "W"://指定宽，高按比例                     
                    toheight = originalImage.Height * width / originalImage.Width;
                    break;
                case "H"://指定高，宽按比例 
                    towidth = originalImage.Width * height / originalImage.Height;
                    break;
                case "Cut"://指定高宽裁减（不变形）                 
                    if ((double)originalImage.Width / (double)originalImage.Height > (double)towidth / (double)toheight) {
                        oh = originalImage.Height;
                        ow = originalImage.Height * towidth / toheight;
                        y = 0;
                        x = (originalImage.Width - ow) / 2;
                    }
                    else {
                        ow = originalImage.Width;
                        oh = originalImage.Width * height / towidth;
                        x = 0;
                        y = (originalImage.Height - oh) / 2;
                    }
                    break;
                default:
                    break;
            }

            //新建一个bmp图片 
            Image bitmap = new System.Drawing.Bitmap(towidth, toheight);            

            //新建一个画板 
            Graphics g = System.Drawing.Graphics.FromImage(bitmap);

            //设置高质量插值法 
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;

            //设置高质量,低速度呈现平滑程度 
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            //清空画布并以透明背景色填充 
            g.Clear(Color.Transparent);

            //在指定位置并且按指定大小绘制原图片的指定部分 
            g.DrawImage(originalImage, new Rectangle(0, 0, towidth, toheight),
                new Rectangle(x, y, ow, oh),
                GraphicsUnit.Pixel);

            try {
                byte[] bytes;
                using (MemoryStream ms = new MemoryStream()) {
                    bitmap.Save(ms, originalImage.RawFormat);
                    bytes = ms.ToArray();
                }
                return bytes;
            }
            catch (System.Exception e) {
                throw e;
            }
            finally {
                originalImage.Dispose();
                g.Dispose();
            }
        }

        /// <summary>
        /// 生成条码 Bitmap,自定义条码高度,自定义文字对齐样式
        /// </summary>
        /// <param name="sourceCode"></param>
        /// <param name="barCodeHeight"></param>
        /// <param name="sf"></param>
        /// <returns></returns>
        public static Bitmap GetCode39(string sourceCode, int barCodeHeight=60)
        {           
            string BarCodeText = sourceCode.ToUpper();
            int leftMargin = 5;
            int topMargin = 0;
            int thickLength = 2;
            int narrowLength = 1;
            int intSourceLength = sourceCode.Length;
            string strEncode = "010010100"; //添加起始码“ *”.
            var font = new System.Drawing.Font("Segoe UI", 5);
            string AlphaBet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%*";
            string[] Code39 =
             {
                 /* 0 */ "000110100" , 
                 /* 1 */ "100100001" , 
                 /* 2 */ "001100001" , 
                 /* 3 */ "101100000" ,
                 /* 4 */ "000110001" , 
                 /* 5 */ "100110000" , 
                 /* 6 */ "001110000" , 
                 /* 7 */ "000100101" ,
                 /* 8 */ "100100100" , 
                 /* 9 */ "001100100" , 
                 /* A */ "100001001" , 
                 /* B */ "001001001" ,
                 /* C */ "101001000" , 
                 /* D */ "000011001" , 
                 /* E */ "100011000" , 
                 /* F */ "001011000" ,
                 /* G */ "000001101" , 
                 /* H */ "100001100" , 
                 /* I */ "001001100" , 
                 /* J */ "000011100" ,
                 /* K */ "100000011" , 
                 /* L */ "001000011" , 
                 /* M */ "101000010" , 
                 /* N */ "000010011" ,
                 /* O */ "100010010" , 
                 /* P */ "001010010" , 
                 /* Q */ "000000111" , 
                 /* R */ "100000110" ,
                 /* S */ "001000110" , 
                 /* T */ "000010110" , 
                 /* U */ "110000001" , 
                 /* V */ "011000001" ,
                 /* W */ "111000000" , 
                 /* X */ "010010001" , 
                 /* Y */ "110010000" , 
                 /* Z */ "011010000" ,
                 /* - */ "010000101" , 
                 /* . */ "110000100" , 
                 /*' '*/ "011000100" ,
                 /* $ */ "010101000" ,
                 /* / */ "010100010" , 
                 /* + */ "010001010" , 
                 /* % */ "000101010" , 
                 /* * */ "010010100"  
             };
            sourceCode = sourceCode.ToUpper();
            Bitmap objBitmap = new Bitmap(((thickLength * 3 + narrowLength * 7) * (intSourceLength + 2)) +
                                           (leftMargin * 2), barCodeHeight + (topMargin * 2));
            Graphics objGraphics = Graphics.FromImage(objBitmap);
            objGraphics.FillRectangle(Brushes.White, 0, 0, objBitmap.Width, objBitmap.Height);
            for (int i = 0; i < intSourceLength; i++) {
                //非法字符校验
                if (AlphaBet.IndexOf(sourceCode[i]) == -1 || sourceCode[i] == '*') {
                    objGraphics.DrawString("Invalid Bar Code", SystemFonts.DefaultFont, Brushes.Red, leftMargin, topMargin);
                    return objBitmap;
                }
                //编码
                strEncode = string.Format("{0}0{1}", strEncode,
                Code39[AlphaBet.IndexOf(sourceCode[i])]);
            }
            strEncode = string.Format("{0}0010010100", strEncode); //添加结束码“*”
            int intEncodeLength = strEncode.Length;
            int intBarWidth;
            for (int i = 0; i < intEncodeLength; i++) //绘制 Code39 barcode
            {
                intBarWidth = strEncode[i] == '1' ? thickLength : narrowLength;
                objGraphics.FillRectangle(i % 2 == 0 ? Brushes.Black : Brushes.White, leftMargin, topMargin, intBarWidth, barCodeHeight);
                leftMargin += intBarWidth;
            }
            //绘制明码         
            //Font barCodeTextFont = new Font("黑体", 10F);
            //RectangleF rect = new RectangleF(2, barCodeHeight - 20, objBitmap.Width - 4, 20);
            //objGraphics.FillRectangle(Brushes.White, rect);
            //文本对齐
            //StringFormat sf = new StringFormat();
            //sf.Alignment = StringAlignment.Center;
            //objGraphics.DrawString(BarCodeText, barCodeTextFont, Brushes.Black, rect, sf);
            return objBitmap;
        }
 
    }
}
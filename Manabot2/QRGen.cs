using Mirai.CSharp.HttpApi.Models.ChatMessages;
using Mirai.CSharp.Models;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static QRCoder.PayloadGenerator;

namespace Manabot2
{
    internal class QRGen
    {
        public static byte[] Url2PNGByte(string url)
        {
            Url generator = new Url(url);
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(generator.ToString(), QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(20);
        }

        public static string Url2BitString(string url)
        {
            Url generator = new Url(url);
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(generator.ToString(), QRCodeGenerator.ECCLevel.L);
            AsciiQRCode qrCode = new AsciiQRCode(qrCodeData);
            return qrCode.GetGraphic(1, "██", "  ");
        }

        public static IImageMessage Url2ImageMessage(string url)
        {
            using (var ms = new MemoryStream(Url2PNGByte(url)))
            {
                return (IImageMessage)Global.qqsession.UploadPictureAsync(UploadTarget.Group, ms).Result;
            }
        }
    }
}

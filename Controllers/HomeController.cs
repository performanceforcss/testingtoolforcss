using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;

using Newtonsoft.Json;

namespace Portal.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult GetScreenshots(string requestUrl)
        {
            var requestId = Guid.NewGuid();

            string path = $@"c:\temp\bat\{requestId}";
            string fileName = $"request.bat";
            string fullPath = $@"{path}\{fileName}";

            var folder = System.IO.Directory.CreateDirectory(path);

            if (!System.IO.File.Exists(fullPath))
            {
                var lines = new List<string>();
                lines.Add($@"cd {path}");
                lines.Add($@"node C:\Users\Vincent\Desktop\Villareal-Zabala\webtest\test\google-test.js requestid={requestId} requesturl={requestUrl}");

                System.IO.File.AppendAllLines(fullPath, lines);
            }
            
            var p = Process.Start(fullPath);
                p.WaitForExit();

            var basePath = HttpRuntime.AppDomainAppPath;
            var resultContentPath = @"Content\ResultImages";
            var resultFullPath = $@"{basePath}\{resultContentPath}";

            var resultFFolder = System.IO.Directory.CreateDirectory(resultFullPath);

            var resultImages = new Dictionary<string, string>();
            var mergedImages = MergeImage(folder, requestId);

            foreach (var mergedImage in mergedImages)
            {
                using (var image = System.Drawing.Image.FromFile($"{mergedImage.Value}"))
                {
                    var resultFileName = $@"{requestId}_{mergedImage.Key}.png";

                    image.Save($@"{resultFFolder.FullName}\{resultFileName}");

                    resultImages.Add(mergedImage.Key, resultFileName);
                }
            }
            
            return Json(new { Id = requestId, Images = resultImages }, JsonRequestBehavior.AllowGet);
        }

        private Dictionary<string, string> MergeImage(System.IO.DirectoryInfo di, Guid requestId)
        {
            var resultFiles = new Dictionary<string, string>();
            var resultDir = di.EnumerateDirectories($"{requestId}").FirstOrDefault();
            
            if (resultDir != null)
            {
                foreach (var dir in resultDir.EnumerateDirectories())
                {
                    var imageFiles = dir.EnumerateFiles("*.png").ToList();
                    var manifestFile = dir.EnumerateFiles($@"{dir.Name}.json").FirstOrDefault();

                    if (imageFiles.Any() && manifestFile != null)
                    {
                        var ssInfo = JsonConvert.DeserializeObject<ScreenshotDetail>(System.IO.File.ReadAllText(manifestFile.FullName));

                        var imageInfo = imageFiles.First();
                        var image = System.Drawing.Image.FromFile($"{imageInfo.FullName}");
                        
                        var width = image.Width;
                        var height = image.Height;
                        var canvasFullHeight = image.Height * imageFiles.Count;

                        var resutingImage = new System.Drawing.Bitmap(width, canvasFullHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        using (var g = System.Drawing.Graphics.FromImage(resutingImage))
                        {
                            var index = 0;
                            var accumulatedHeight = 0d;

                            foreach (var imgInf in imageFiles)
                            {
                                var posY = index * height;

                                var img = System.Drawing.Image.FromFile($"{imgInf.FullName}");
                                var advanceAccumulatedHeight = accumulatedHeight + ssInfo.WindowHeight;

                                if (advanceAccumulatedHeight > ssInfo.DocHeight)
                                {
                                    var ratio = height / ssInfo.WindowHeight;
                                    var offset = (ssInfo.DocHeight - accumulatedHeight) * ratio;

                                    var cropRect = new System.Drawing.Rectangle(0, (int)Math.Ceiling((offset - height) * -1), width, (int)Math.Ceiling(offset));
                                    var cropedImg = new System.Drawing.Bitmap(cropRect.Width, cropRect.Height);

                                    using (var gc = System.Drawing.Graphics.FromImage(cropedImg))
                                    {
                                        gc.DrawImage(
                                            img, 
                                            new System.Drawing.Rectangle(0, 0, cropedImg.Width, cropedImg.Height),
                                            cropRect,
                                            System.Drawing.GraphicsUnit.Pixel);
                                    }

                                    g.DrawImage(cropedImg, new System.Drawing.Point(0, posY));

                                    cropedImg.Dispose();
                                }
                                else
                                {
                                    g.DrawImage(img, new System.Drawing.Point(0, posY));
                                }

                                

                                img.Dispose();

                                index++;
                                accumulatedHeight = advanceAccumulatedHeight;
                            }
                        }

                        image.Dispose();

                        var filepath = $@"{di.FullName}\{dir.Name}.png";

                        resutingImage.Save(filepath);
                        resutingImage.Dispose();

                        resultFiles.Add(dir.Name, filepath);
                    }
                }
            }

            return resultFiles;
        }
    }

    public class ScreenshotDetail
    {
        [JsonProperty("windowHeight")]
        public double WindowHeight { get; set; }

        [JsonProperty("windowWidth")]
        public double WindowWidth { get; set; }

        [JsonProperty("docHeight")]
        public double DocHeight { get; set; }
    }
}
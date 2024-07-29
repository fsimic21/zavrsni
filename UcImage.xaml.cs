using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.OCR;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Microsoft.Win32;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Tesseract;
using EntitiesLayer;
using ServiceLayer;
using System.Windows.Media;
using System.Text.RegularExpressions;

namespace PresentationLayer {
    public partial class UcImage : UserControl {

        private readonly CarService _service;

        public UcImage(CarService service) {
            _service = service;
            InitializeComponent();
        }

        private void LoadImageButton_Click(object sender, System.Windows.RoutedEventArgs e) {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true) {
                string filePath = openFileDialog.FileName;
                DisplayImage(filePath);
                ProcessAndDisplayGrayImage(filePath);
                DetectCarColor(filePath);
                CheckCar();
            }
        }

        private void CheckCar() {
            var car = new Car {
                Id = 0,
                Color = ColorTextBox.Text,
                LicencePlate = LicenceTextBox.Text
            };
            var carList = _service.GetAllCars();
            bool carExists = carList.Any(c => c.LicencePlate == car.LicencePlate && c.Color == car.Color);
            LicenceDetectionResult.Background = carExists ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
            LicenceDetectionResult.Text = carExists ? "Passed" : "Reject";
        }

        private void DisplayImage(string filePath) {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.EndInit();
            LoadedImage.Source = bitmap;
        }

        private void ProcessAndDisplayGrayImage(string filePath) {
            Mat img = CvInvoke.Imread(filePath, ImreadModes.Color);
            Mat gray = ProcessImage(img);
            BitmapImage bitmapImage = ConvertMatToBitmapImage(gray);
            GrayImage.Source = bitmapImage;
            ExtractLicensePlateText(BitmapImageToBitmap(bitmapImage));
        }

        private void ExtractLicensePlateText(Bitmap image) {
            int[] angles = { 0, 45, -45 };
            string longestText = string.Empty;

            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default)) {
                foreach (int angle in angles) {
                    using (var rotatedImage = RotateImage(image, angle))
                    using (var pix = PixConverter.ToPix(rotatedImage))
                    using (var page = engine.Process(pix)) {
                        string text = page.GetText();
                        if (text.Length > longestText.Length) {
                            longestText = text;
                        }
                    }
                }
            }

            string filteredText = Regex.Replace(longestText, @"[^a-zA-Z0-9]", string.Empty);
            LicenceTextBox.Text = filteredText;
        }

        private Bitmap RotateImage(Bitmap image, float angle) {
            var rotatedBmp = new Bitmap(image.Width, image.Height);
            rotatedBmp.SetResolution(image.HorizontalResolution, image.VerticalResolution);
            using (Graphics g = Graphics.FromImage(rotatedBmp)) {
                g.TranslateTransform(image.Width / 2f, image.Height / 2f);
                g.RotateTransform(angle);
                g.TranslateTransform(-image.Width / 2f, -image.Height / 2f);
                g.DrawImage(image, new Point(0, 0));
            }

            return rotatedBmp;
        }

        private Bitmap BitmapImageToBitmap(BitmapImage bitmapImage) {
            using (var outStream = new MemoryStream()) {
                var enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                return new Bitmap(outStream);
            }
        }

        private BitmapImage ConvertMatToBitmapImage(Mat mat) {
            using (var memoryStream = new MemoryStream()) {
                mat.ToBitmap().Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }

        private void DetectCarColor(string filePath) {
            var dominantColor = GetDominantColorUsingImageSharp(filePath);
            var color = Color.FromArgb(dominantColor.R, dominantColor.G, dominantColor.B);
            ColorTextBox.Text = ColorTranslator.ToHtml(color);
            ColorTextBox.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(dominantColor.R, dominantColor.G, dominantColor.B));
        }

        private Rgb24 GetDominantColorUsingImageSharp(string filePath) {
            using (var image = SixLabors.ImageSharp.Image.Load<Rgb24>(filePath)) {
                image.Mutate(x => x
                    .Resize(new ResizeOptions { Sampler = KnownResamplers.NearestNeighbor, Size = new SixLabors.ImageSharp.Size(100, 0) })
                    .Quantize(new OctreeQuantizer(new QuantizerOptions { Dither = null })));

                return image[0, 0];
            }
        }

        private Mat ProcessImage(Mat img) {
            var gray = new UMat();
            CvInvoke.CvtColor(img, gray, ColorConversion.Bgr2Gray);
            CvInvoke.GaussianBlur(gray, gray, new Size(3, 3), 1);
            var cannyEdges = new UMat();
            CvInvoke.Canny(gray, cannyEdges, 180.0, 120.0);

            var boxList = new List<RotatedRect>();
            using (var contours = new VectorOfVectorOfPoint()) {
                CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
                for (int i = 0; i < contours.Size; i++) {
                    using (var contour = contours[i])
                    using (var approxContour = new VectorOfPoint()) {
                        CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.05, true);
                        if (CvInvoke.ContourArea(approxContour, false) > 250 && approxContour.Size == 4) {
                            bool isRectangle = approxContour.ToArray().Select((p, j) => Math.Abs(
                                Emgu.CV.PointCollection.PolyLine(approxContour.ToArray(), true)[(j + 1) % 4].GetExteriorAngleDegree(
                                    Emgu.CV.PointCollection.PolyLine(approxContour.ToArray(), true)[j]))).All(angle => angle >= 80 && angle <= 100);
                            if (isRectangle) boxList.Add(CvInvoke.MinAreaRect(approxContour));
                        }
                    }
                }
            }

            var largestBox = boxList.OrderByDescending(box => box.Size.Width * box.Size.Height).FirstOrDefault();
            return largestBox.Equals(default(RotatedRect)) ? img : new Mat(img, CvInvoke.BoundingRectangle(largestBox.GetVertices().Select(Point.Round).ToArray()));
        }
    }
}

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
using System.Drawing;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using System.IO;
using System.Text.RegularExpressions;
using ServiceLayer;
using EntitiesLayer;
using Tesseract;

namespace PresentationLayer {
    public partial class AddCar : UserControl {
        CarService service;
        int num;

        public AddCar(CarService service, int num) {
            this.service = service;
            this.num = num;
            InitializeComponent();

        }

        private void LoadImageButton_Click(object sender, System.Windows.RoutedEventArgs e) {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true) {
                string filePath = openFileDialog.FileName;
                DisplayImage(filePath);
                GrayImageDisplay(filePath);
                DetectCarColour(filePath);
            }
        }

        private void DisplayImage(string filePath) {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.EndInit();
            LoadedImage.Source = bitmap;
        }

        private void GrayImageDisplay(string filePath) {
            Mat img = CvInvoke.Imread(filePath, ImreadModes.Color);
            Mat gray = ProcessImage(img);
            BitmapImage bitmapImage = ConvertMatToBitmapImage(gray);
            GrayImage.Source = bitmapImage;
            GetLicensePlate(BitmapImage2Bitmap(bitmapImage));
        }

        private void GetLicensePlate(Bitmap image) {
            int[] angles = { 0, 45, -45 };
            string longestText = string.Empty;

            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default)) {
                foreach (int angle in angles) {
                    Bitmap rotatedImage = RotateImage(image, angle);

                    using (var pix = PixConverter.ToPix(rotatedImage)) {
                        using (var page = engine.Process(pix)) {
                            string text = page.GetText();

                            if (text.Length > longestText.Length) {
                                longestText = text;
                            }
                        }
                    }
                }

                string filteredText = System.Text.RegularExpressions.Regex.Replace(longestText, @"[^a-zA-Z0-9]", string.Empty);
                LicenceTextBox.Text = filteredText;
            }
        }

        private Bitmap RotateImage(Bitmap image, float angle) {
            Bitmap rotatedBmp = new Bitmap(image.Width, image.Height);
            rotatedBmp.SetResolution(image.HorizontalResolution, image.VerticalResolution);
            using (Graphics g = Graphics.FromImage(rotatedBmp)) {
                g.TranslateTransform((float)image.Width / 2, (float)image.Height / 2);
                g.RotateTransform(angle);
                g.TranslateTransform(-(float)image.Width / 2, -(float)image.Height / 2);
                g.DrawImage(image, new Point(0, 0));
            }

            return rotatedBmp;
        }



        private Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage) {

            using (MemoryStream outStream = new MemoryStream()) {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                Bitmap bitmap = new Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }



        private BitmapImage ConvertMatToBitmapImage(Mat mat) {
            using (System.IO.MemoryStream memoryStream = new System.IO.MemoryStream()) {
                Bitmap bitmap = mat.ToBitmap();
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }

        private void DetectCarColour(string filePath) {
            Rgb24 dominantColor = GetDominantColorUsingImageSharp(filePath);
            Color color = Color.FromArgb(dominantColor.R, dominantColor.G, dominantColor.B);
            ColorTextBox.Text = ColorTranslator.ToHtml(color);
            ColorTextBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(dominantColor.R, dominantColor.G, dominantColor.B));
        }

        private Rgb24 GetDominantColorUsingImageSharp(string filePath) {
            using (var image = SixLabors.ImageSharp.Image.Load<Rgb24>(filePath)) {
                image.Mutate(x => x
                    .Resize(new ResizeOptions() { Sampler = KnownResamplers.NearestNeighbor, Size = new SixLabors.ImageSharp.Size(100, 0) })
                    .Quantize(new OctreeQuantizer(new QuantizerOptions { Dither = null })));

                return image[0, 0];
            }
        }



        public Mat ProcessImage(Mat img) {
            using (UMat gray = new UMat())
            using (UMat cannyEdges = new UMat()) {
                CvInvoke.CvtColor(img, gray, ColorConversion.Bgr2Gray);
                CvInvoke.GaussianBlur(gray, gray, new Size(3, 3), 1);

                double cannyThreshold = 180.0;
                double cannyThresholdLinking = 120.0;
                CvInvoke.Canny(gray, cannyEdges, cannyThreshold, cannyThresholdLinking);

                List<RotatedRect> boxList = new List<RotatedRect>();
                using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint()) {
                    CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
                    int count = contours.Size;
                    for (int i = 0; i < count; i++) {
                        using (VectorOfPoint contour = contours[i])
                        using (VectorOfPoint approxContour = new VectorOfPoint()) {
                            CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.05, true);
                            if (CvInvoke.ContourArea(approxContour, false) > 250) {
                                if (approxContour.Size == 4) {
                                    bool isRectangle = true;
                                    Point[] pts = approxContour.ToArray();
                                    LineSegment2D[] edges = PointCollection.PolyLine(pts, true);
                                    for (int j = 0; j < edges.Length; j++) {
                                        double angle = Math.Abs(edges[(j + 1) % edges.Length].GetExteriorAngleDegree(edges[j]));
                                        if (angle < 80 || angle > 100) {
                                            isRectangle = false;
                                            break;
                                        }
                                    }

                                    if (isRectangle) boxList.Add(CvInvoke.MinAreaRect(approxContour));
                                }
                            }
                        }
                    }
                }

                RotatedRect largestBox = new RotatedRect();
                double largestArea = 0;
                foreach (RotatedRect box in boxList) {
                    double area = box.Size.Width * box.Size.Height;
                    if (area > largestArea) {
                        largestArea = area;
                        largestBox = box;
                    }
                }
                Mat result = img;
                if (largestArea > 0) {
                    PointF[] vertices = largestBox.GetVertices();
                    Point[] points = Array.ConvertAll(vertices, Point.Round);
                    Rectangle boundingRect = CvInvoke.BoundingRectangle(points);

                    result = new Mat(img, boundingRect);
                }

                if (result.Width > 499) {
                    double scaleFactor = 499.0 / result.Width;
                    Size newSize = new Size(499, (int)(result.Height * scaleFactor));
                    CvInvoke.Resize(result, result, newSize);
                }

                return result;
            }
        }

        private void AddCarButton_Click(object sender, System.Windows.RoutedEventArgs e) {
            Car car = new Car();
            car.Color = ColorTextBox.Text.ToString();
            car.LicencePlate = LicenceTextBox.Text.ToString();
            car.Id = num+1;
            service.AddCar(car);
            UcManager screen = new UcManager(service);
            this.Content = screen;
        }
    }
}

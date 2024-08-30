using Emgu.CV;
using Microsoft.Win32;
using System.Drawing;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using EntitiesLayer;
using ServiceLayer;
using System.Windows.Media;

namespace PresentationLayer {
    public partial class UcImage : UserControl {

        private readonly CarService _service;

        private LicensePlateUtil plateUtil;
        public UcImage(CarService service) {
            _service = service;
            plateUtil = new LicensePlateUtil();
            InitializeComponent();
        }
        private void LoadImageButton_Click(object sender, System.Windows.RoutedEventArgs e) {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true) {
                string filePath = openFileDialog.FileName;
                DisplayImage(filePath);
                var grayImage = plateUtil.ProcessImage(filePath);
                GrayImage.Source = ConvertMatToBitmapImage(grayImage);
                LicenceTextBox.Text = plateUtil.ExtractLicensePlateText(grayImage);
                var carColor = plateUtil.DetectCarColor(filePath);
                ColorTextBox.Text = ColorTranslator.ToHtml(carColor);
                ColorTextBox.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(carColor.R, carColor.G, carColor.B));
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
    }
}

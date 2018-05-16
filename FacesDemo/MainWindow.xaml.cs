using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ProjectOxford.Common.Contract;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

namespace FacesDemo
{
    public partial class MainWindow : Window
    {
        /*
         * 1. Replace the first parameter with your valid subscription key.
         * 2. Replace or verify the region in the second parameter.
         */
        private readonly IFaceServiceClient _faceServiceClient =
            new FaceServiceClient("ae3a3d45181348cc80f1f602256232da", "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");

        private Face[] _faces;                   // The list of detected faces
        private string[] _faceDescriptions;      // The list of descriptions for the detected faces
        private double _resizeFactor;            // The resize factor for the displayed image

        public MainWindow()
        {
            InitializeComponent();
        }

        /*
         * Displays the image and calls Detect Faces
         */
        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the image file to scan from the user - allowed jpg, png, jpeg
            var openDlg = new Microsoft.Win32.OpenFileDialog {Filter = "Image Files|*.jpg;*.png;*.jpeg"};
            var result = openDlg.ShowDialog(this);

            // Return if canceled
            if (!(bool)result)
            {
                return;
            }

            // Display the image file
            var filePath = openDlg.FileName;
            var fileUri = new Uri(filePath);
            var bitmapSource = new BitmapImage();
            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();
            FacePhoto.Source = bitmapSource;

            // Detect any faces in the image
            Title = "Detecting...";
            _faces = await UploadAndDetectFaces(filePath);
            Title = $"Detection Finished. {_faces.Length} face(s) detected";

            if (_faces.Length <= 0) return;
            // Prepare to draw rectangles around the faces
            var visual = new DrawingVisual();
            var drawingContext = visual.RenderOpen();
            drawingContext.DrawImage(bitmapSource,
                new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));
            var dpi = bitmapSource.DpiX;
            _resizeFactor = 96 / dpi;
            _faceDescriptions = new string[_faces.Length];

            for (var i = 0; i < _faces.Length; ++i)
            {
                var face = _faces[i];
                // Draw a rectangle on the face
                drawingContext.DrawRectangle(
                    Brushes.Transparent,
                    new Pen(Brushes.Red, 2),
                    new Rect(
                        face.FaceRectangle.Left * _resizeFactor,
                        face.FaceRectangle.Top * _resizeFactor,
                        face.FaceRectangle.Width * _resizeFactor,
                        face.FaceRectangle.Height * _resizeFactor
                    )
                );
                // Store the face description
                _faceDescriptions[i] = FaceDescription(face);
            }
            drawingContext.Close();

            // Display the image with the rectangle around the face
            var faceWithRectBitmap = new RenderTargetBitmap((int)(bitmapSource.PixelWidth * _resizeFactor),(int)(bitmapSource.PixelHeight * _resizeFactor),96,96,PixelFormats.Pbgra32);

            faceWithRectBitmap.Render(visual);
            FacePhoto.Source = faceWithRectBitmap;
            // Set the status bar text
            faceDescriptionStatusBar.Text = "Place the mouse pointer over a face to see the face description.";
        }
        

        // Displays the face description when the mouse is over a face rectangle
        private void FacePhoto_MouseMove(object sender, MouseEventArgs e)
        {
            // If the REST call has not completed, return from this method
            if (_faces == null)
                return;
            // Find the mouse position relative to the image
            var mouseXy = e.GetPosition(FacePhoto);
            var imageSource = FacePhoto.Source;
            var bitmapSource = (BitmapSource)imageSource;
            // Scale adjustment between the actual size and displayed size
            var scale = FacePhoto.ActualWidth / (bitmapSource.PixelWidth / _resizeFactor);

            // Check if this mouse position is over a face rectangle
            var mouseOverFace = false;

            for (var i = 0; i < _faces.Length; ++i)
            {
                var fr = _faces[i].FaceRectangle;
                var left = fr.Left * scale;
                var top = fr.Top * scale;
                var width = fr.Width * scale;
                var height = fr.Height * scale;

                // Display the face description for this face if the mouse is over this face rectangle
                if (!(mouseXy.X >= left) || !(mouseXy.X <= left + width) || !(mouseXy.Y >= top) ||
                    !(mouseXy.Y <= top + height)) continue;
                faceDescriptionStatusBar.Text = _faceDescriptions[i];
                mouseOverFace = true;
                break;
            }

            // If the mouse is not over a face rectangle
            if (!mouseOverFace)
                faceDescriptionStatusBar.Text = "Place the mouse pointer over a face to see the face description.";
        }

        /*
         *Uploads the image file and calls Detect Faces
         */
        private async Task<Face[]> UploadAndDetectFaces(string imageFilePath)
        {
            // The list of Face attributes to return.
            IEnumerable<FaceAttributeType> faceAttributes =
                new[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Emotion, FaceAttributeType.Glasses, FaceAttributeType.Hair };

            // Call the Face API.
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    Face[] faces = await _faceServiceClient.DetectAsync(imageFileStream, true, false, faceAttributes);
                    return faces;
                }
            }
            // Catch and display Face API errors.
            catch (FaceAPIException f)
            {
                MessageBox.Show(f.ErrorMessage, f.ErrorCode);
                return new Face[0];
            }
            // Catch and display all other errors.
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                return new Face[0];
            }
        }

        /*
         *Returns a string that describes the given face
         */
        private string FaceDescription(Face face)
        {
            var sb = new StringBuilder();
            sb.Append("Face: ");

            // Add the gender, age, and smile
            sb.Append(face.FaceAttributes.Gender);
            sb.Append(", ");
            sb.Append(face.FaceAttributes.Age);
            sb.Append(", ");
            sb.Append($"smile {face.FaceAttributes.Smile * 100:F1}%, ");

            // Add the emotions. Display all emotions over 10%
            sb.Append("Emotion: ");
            var emotionScores = face.FaceAttributes.Emotion;
            if (emotionScores.Anger >= 0.1f) sb.Append($"anger {emotionScores.Anger * 100:F1}%, ");
            if (emotionScores.Contempt >= 0.1f) sb.Append($"contempt {emotionScores.Contempt * 100:F1}%, ");
            if (emotionScores.Disgust >= 0.1f) sb.Append($"disgust {emotionScores.Disgust * 100:F1}%, ");
            if (emotionScores.Fear >= 0.1f) sb.Append($"fear {emotionScores.Fear * 100:F1}%, ");
            if (emotionScores.Happiness >= 0.1f) sb.Append($"happiness {emotionScores.Happiness * 100:F1}%, ");
            if (emotionScores.Neutral >= 0.1f) sb.Append($"neutral {emotionScores.Neutral * 100:F1}%, ");
            if (emotionScores.Sadness >= 0.1f) sb.Append($"sadness {emotionScores.Sadness * 100:F1}%, ");
            if (emotionScores.Surprise >= 0.1f) sb.Append($"surprise {emotionScores.Surprise * 100:F1}%, ");

            // Add glasses
            sb.Append(face.FaceAttributes.Glasses);
            sb.Append(", ");

            // Add hair
            sb.Append("Hair: ");

            // Display baldness confidence if over 1%
            if (face.FaceAttributes.Hair.Bald >= 0.01f)
                sb.Append($"bald {face.FaceAttributes.Hair.Bald * 100:F1}% ");
            // Display all hair color attributes over 10%
            var hairColors = face.FaceAttributes.Hair.HairColor;
            foreach (var hairColor in hairColors)
            {
                if (!(hairColor.Confidence >= 0.1f)) continue;
                sb.Append(hairColor.Color.ToString());
                sb.Append($" {hairColor.Confidence * 100:F1}% ");
            }
            // Return the built string
            return sb.ToString();
        }
    }
}
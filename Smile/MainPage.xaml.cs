﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using WindowsPreview.Kinect;
using Microsoft.Kinect.Face;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml.Media;
using Windows.UI;
using Windows.Storage;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Smile
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private bool cameraSoundLoaded = false;
        private DispatcherTimer cameraTimer;
        private DispatcherTimer faceInLensCheckingTimer;
        private DispatcherTimer engagementTimer;
        private int countdownValue = 3;

        private KinectSensor sensor = null;
        private MultiSourceFrameReader multiSrcFrameReader = null;
        private FrameDescription curColorFrameDescription = null;

        IList<Body> bodies = null;
        FaceFrameSource faceSrc = null;
        FaceFrameReader faceReader = null;
        FaceFrameResult curFaceResult = null;
        RectI faceBoundsInColorSpace;
        private bool showFaceBoundaryBox = true;

        private WriteableBitmap curColorBitmap = null;
        private StorageFile curSnapshot = null;
        private int notSmilingCount = 0;

        private Random rnd = new Random();

        public string CountdownVal
        {
            get
            {
                return countdownValue.ToString();
            }
        }


        public MainPage()
        {
            this.sensor = KinectSensor.GetDefault();
            this.multiSrcFrameReader = this.sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Body | FrameSourceTypes.Color);
            this.multiSrcFrameReader.MultiSourceFrameArrived += MultiSrcFrameReader_MultiSourceFrameArrived;

            

            this.curColorFrameDescription = this.sensor.ColorFrameSource.FrameDescription;
            this.curColorBitmap = new WriteableBitmap(this.curColorFrameDescription.Width, this.curColorFrameDescription.Height);
            this.bodies = new Body[this.sensor.BodyFrameSource.BodyCount];
            this.faceSrc = new FaceFrameSource(this.sensor, 0, FaceFrameFeatures.BoundingBoxInColorSpace |
                                                               FaceFrameFeatures.FaceEngagement |
                                                               FaceFrameFeatures.Glasses |
                                                               FaceFrameFeatures.Happy |
                                                               FaceFrameFeatures.LookingAway |
                                                               FaceFrameFeatures.MouthOpen);
            this.faceReader = this.faceSrc.OpenReader();
            this.faceReader.FrameArrived += FaceReader_FrameArrived;

            this.sensor.Open();

            this.DataContext = this;
            this.InitializeComponent();
        }

        private void FaceReader_FrameArrived(FaceFrameReader sender, FaceFrameArrivedEventArgs args)
        {
            if (this.showFaceBoundaryBox)
            {
                using (FaceFrame frame = args.FrameReference.AcquireFrame())
                {
                    if (frame == null) return;
                    curFaceResult = frame.FaceFrameResult;
                    if (curFaceResult == null) return;
                    this.faceBoundsInColorSpace = curFaceResult.FaceBoundingBoxInColorSpace;

                    if ((this.faceBoundsInColorSpace.Left > 0) && (this.faceBoundsInColorSpace.Top > 0) &&
                        (this.faceBoundsInColorSpace.Right > 0) && (this.faceBoundsInColorSpace.Bottom > 0))
                    {
                        this.FaceBoundary.Width = (this.faceBoundsInColorSpace.Right - this.faceBoundsInColorSpace.Left);
                        this.FaceBoundary.Height = (this.faceBoundsInColorSpace.Bottom - this.faceBoundsInColorSpace.Top);
                        Canvas.SetLeft(this.FaceBoundary, this.faceBoundsInColorSpace.Left);
                        Canvas.SetTop(this.FaceBoundary, this.faceBoundsInColorSpace.Top);
                        this.FaceBoundary.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        this.FaceBoundary.Visibility = Visibility.Collapsed;
                    }
                }
            }
            
        }

        private void MultiSrcFrameReader_MultiSourceFrameArrived(MultiSourceFrameReader sender, MultiSourceFrameArrivedEventArgs args)
        {
            MultiSourceFrame frame = args.FrameReference.AcquireFrame();
            if (frame == null) return;

            using (BodyFrame bodyFrame = frame.BodyFrameReference.AcquireFrame())
            {
                if (bodyFrame == null) return;
                bodyFrame.GetAndRefreshBodyData(this.bodies);
                Body firstTrackedBody = this.bodies.Where(b => b.IsTracked).FirstOrDefault();

                if ((!this.faceSrc.IsTrackingIdValid) && (firstTrackedBody != null))
                {
                    this.faceSrc.TrackingId = firstTrackedBody.TrackingId;
                }
            }

            using (ColorFrame colorFrame = frame.ColorFrameReference.AcquireFrame())
            {
                if (colorFrame == null) return;
                this.curColorBitmap = colorFrame.ToBitmap();
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            this.FadeInText.Begin();
        }

        private void FadeInText_Completed(object sender, object e)
        {
            //Delay and show Camera  TODO...need to wait until user is "engaged"...not just delay
            this.engagementTimer = new DispatcherTimer();
            this.engagementTimer.Tick += EngagementTimer_Tick;
            this.engagementTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            this.engagementTimer.Start();

            //Pause(10000);
            /*this.SmileTitle.Opacity = 0;
            this.PolaroidLogo.Opacity = 0;
            FadeInCamera.Begin();*/
        }

        private void EngagementTimer_Tick(object sender, object e)
        {
            if (curFaceResult == null) return;

            DetectionResult isLookingAway = curFaceResult.FaceProperties[FaceProperty.LookingAway];
            DetectionResult isEngaged = curFaceResult.FaceProperties[FaceProperty.Engaged];
            if ((isEngaged == DetectionResult.Yes) || (isEngaged == DetectionResult.Maybe))
            {
                this.engagementTimer.Stop();
                this.SmileTitle.Opacity = 0;
                this.FadeInCamera.Begin();
            }
        }

        private async void Pause(int millsDelay)
        {
            await Task.Delay(millsDelay);
        }

        private void CameraTimer_Tick(object sender, object e)
        {
            if (this.countdownValue == 1)
            {
                //take picture
                this.CountdownValue.Opacity = 0;
                this.FaceBoundary.Visibility = Visibility.Collapsed;
                this.countdownValue = 3;
                this.cameraTimer.Stop();

                if (this.cameraSoundLoaded)
                {
                    this.CameraSound.Play();
                }
                this.FlashEffectCanvas.Visibility = Visibility.Visible;
                this.CameraImage.Opacity = 0;
                this.FlashFade.Begin();
            }
            else
            {
                --this.countdownValue;
                this.CountdownValue.Text = this.CountdownVal;
            }
        }

        private void FadeInCamera_Completed(object sender, object e)
        {

            //start timer to check if user's face is within the lens area of the background
            this.Instructions.Text = "Smile!";
            this.Instructions.Opacity = 1;
            this.faceInLensCheckingTimer = new DispatcherTimer();
            this.faceInLensCheckingTimer.Tick += FaceInLensCheckingTimer_Tick;
            this.faceInLensCheckingTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);  //500 ms
            this.faceInLensCheckingTimer.Start();
        }

        private void TakePicture()
        {
            this.notSmilingCount = 0;
            if (this.showFaceBoundaryBox)
            {
                this.FaceBoundary.Fill = new SolidColorBrush(Colors.GreenYellow);
            }
            this.faceInLensCheckingTimer.Stop();

            this.Instructions.Opacity = 0;
            this.CountdownValue.Text = this.CountdownVal;
            this.CountdownValue.Opacity = 1;
            cameraTimer = new DispatcherTimer();
            cameraTimer.Tick += CameraTimer_Tick;
            cameraTimer.Interval = new TimeSpan(0, 0, 1);
            cameraTimer.Start();
        }

        private void FaceInLensCheckingTimer_Tick(object sender, object e)
        {
            System.Diagnostics.Debug.WriteLine("Face Bounds: Left = " + this.faceBoundsInColorSpace.Left.ToString() + " Right = " + this.faceBoundsInColorSpace.Right.ToString() + " Top = " + this.faceBoundsInColorSpace.Top.ToString() + " Bottom = " + this.faceBoundsInColorSpace.Bottom.ToString());
            if (curFaceResult == null) return;

            DetectionResult isHappy = curFaceResult.FaceProperties[FaceProperty.Happy];
            if ((isHappy == DetectionResult.Maybe) || (isHappy == DetectionResult.Yes))
            {
                TakePicture();
            }
            else
            {
                this.notSmilingCount += 1;
                if (this.notSmilingCount >= 16)
                {
                    this.Instructions.Text = "Fine. Whatever.";
                    TakePicture();
                }
                else if (this.notSmilingCount >= 8)
                {
                    this.Instructions.Text = "Come on! Smile.";
                }
            }

        }

        private async void FlashFade_Completed(object sender, object e)
        {
            //crop and create composite image to show
            WriteableBitmap original = await BitmapFactory.New(1, 1).FromPixelBuffer(this.curColorBitmap.PixelBuffer, this.curColorBitmap.PixelWidth, this.curColorBitmap.PixelHeight);
            WriteableBitmap background = await BitmapFactory.New(1, 1).FromContent(new System.Uri("ms-appx:///Images/hand_polaroid_black.jpg"));
            WriteableBitmap cropped = original.Crop(this.faceBoundsInColorSpace.Left - 200, this.faceBoundsInColorSpace.Top - 240, 
                                (this.faceBoundsInColorSpace.Right - this.faceBoundsInColorSpace.Left + 400), 
                                (this.faceBoundsInColorSpace.Bottom - this.faceBoundsInColorSpace.Top + 480));
            WriteableBitmap beachBkgrd = await BitmapFactory.New(1, 1).FromContent(new System.Uri("ms-appx:///Images/cannes-background.jpg"));
            WriteableBitmap croppedBeachBkgrd = beachBkgrd.Crop(1130, 1220,
                                (this.faceBoundsInColorSpace.Right - this.faceBoundsInColorSpace.Left + 400),
                                (this.faceBoundsInColorSpace.Bottom - this.faceBoundsInColorSpace.Top + 440));
            WriteableBitmap sizedImage = cropped.Resize(810, 830, WriteableBitmapExtensions.Interpolation.Bilinear);
            
            WriteableBitmap sizedBkgrd = croppedBeachBkgrd.Resize(810, 830, WriteableBitmapExtensions.Interpolation.Bilinear);
            //background.Blit(new Rect(825, 555, 810, 830), sizedBkgrd, new Rect(0, 0, 810, 830), WriteableBitmapExtensions.BlendMode.None);
            background.Blit(new Rect(825, 555, 810, 830), sizedImage, new Rect(0, 0, 810, 830), WriteableBitmapExtensions.BlendMode.None);

            //caption
            WriteableBitmap textBmp = await GetRandomCaption();
            background.Blit(new Rect(900, 1450, 580, 85), textBmp, new Rect(0, 0, 575, 85), WriteableBitmapExtensions.BlendMode.Alpha);

            this.SnappedPicture.Source = background;
            this.FadeInSnappedPicture.Begin();
        }

        private async Task<WriteableBitmap> GetRandomCaption()
        {
            int val = rnd.Next(1, 4);
            switch (val)
            {
                case 1:
                    return await BitmapFactory.New(1, 1).FromContent(new System.Uri("ms-appx:///Images/beunmissable-text.png"));
                case 2:
                    return await BitmapFactory.New(1, 1).FromContent(new System.Uri("ms-appx:///Images/cannes2016-text.png"));
                case 3:
                default:
                    return await BitmapFactory.New(1, 1).FromContent(new System.Uri("ms-appx:///Images/orig-selfie-text.png"));
            }

        }

        private async Task<StorageFile> SaveSnapshot(string fileName, FileFormat format)
        {
            ColorBitmapGenerator cbg = new ColorBitmapGenerator();
            if (this.curColorBitmap != null)
            {
                return await cbg.SaveBitmapToFile(this.curColorBitmap, fileName, format);
            }
            return null;
        }

        private void CameraSound_MediaOpened(object sender, RoutedEventArgs e)
        {
            this.cameraSoundLoaded = true;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            //TODO: cleanup
            this.sensor.Close();
        }
    }
}

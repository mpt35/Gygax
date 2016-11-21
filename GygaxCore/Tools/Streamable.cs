﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using GygaxCore.Interfaces;
using IImage = Emgu.CV.IImage;
using Size = System.Drawing.Size;
using NLog;

namespace GygaxCore.DataStructures
{
    public class Streamable : IStreamable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string Location { get; protected set; }

        public string Name { get; protected set; }

        private object _data;
        public object Data
        {
            get
            {
                return _data;
            }
            set
            {
                _data = value;
                OnPropertyChanged("Data");
            }
        }
        
        private ImageSource _imageSource;
        public ImageSource ImageSource
        {
            get { return _imageSource; }
            private set
            {
                _imageSource = value;
                OnPropertyChanged("ImageSource");
            }
        }

        private IImage _cvSource;
        public IImage CvSource
        {
            get { return _cvSource; }
            set
            {
                _cvSource = value;
                OnPropertyChanged("CvSource");
                if(_cvSource != null)
                    ImageSource = ToBitmapSource(_cvSource);
            }
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
        
        /// <summary>
        /// Delete a GDI object
        /// </summary>
        /// <param name="o">The poniter to the GDI object to be deleted</param>
        /// <returns></returns>
        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        public double ImageScale { get; private set; }

        /// <summary>
        /// Convert an IImage to a WPF BitmapSource. The result can be used in the Set Property of Image.ImageSource
        /// </summary>
        /// <param name="image">The Emgu CV Image</param>
        /// <returns>The equivalent BitmapSource</returns>
        public BitmapSource ToBitmapSource(IImage image)
        {
            ImageScale = 1000.0/image.Bitmap.Width;

            var size = new Size((int)Math.Ceiling(image.Bitmap.Width * ImageScale), (int)Math.Ceiling(image.Bitmap.Height * ImageScale));

            Image<Bgr, Byte> dst = new Image<Bgr, byte>(size);

            CvInvoke.Resize(image,dst,size, ImageScale, ImageScale, Inter.Linear);
            
            using (System.Drawing.Bitmap source = dst.Bitmap)
            {
                IntPtr ptr = source.GetHbitmap(); //obtain the Hbitmap

                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()
                );

                bs.Freeze();

                DeleteObject(ptr); //release the HBitmap
                return bs;
            }
        }

        public virtual void Close()
        {
        }
    }
}

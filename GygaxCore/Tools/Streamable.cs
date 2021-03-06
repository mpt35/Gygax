﻿using System;
using System.ComponentModel;
using System.Globalization;
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
using System.IO;
using System.Linq;

namespace GygaxCore.DataStructures
{
    public class Streamable : IStreamable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string Location { get; set; }

        public string Name { get; set; }

        private object _data;

        public object Data
        {
            get { return _data; }
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
                if (_cvSource != null)
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

            var size = new Size((int) Math.Ceiling(image.Bitmap.Width*ImageScale),
                (int) Math.Ceiling(image.Bitmap.Height*ImageScale));

            Image<Bgr, Byte> dst = new Image<Bgr, byte>(size);

            CvInvoke.Resize(image, dst, size, ImageScale, ImageScale, Inter.Linear);

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

        public delegate void ClosingEvent(IStreamable item);
        public event ClosingEvent OnClosing;

        public virtual void Close()
        {
            OnClosing?.Invoke(this);
        }

        public virtual void Save(string filename)
        {
            try
            {
                CvSource.Save(filename);
            }
            catch (Exception e)
            {
                LogManager.GetCurrentClassLogger().Error(e, "Can not write file "+filename);
            }
            
        }


        public virtual void Save()
        {
            if (Location == null || Location.Equals(""))
                return;

            var path = Path.GetDirectoryName(Location) + @"\";
            var file = Path.GetFileNameWithoutExtension(Location);
            var extension = ".jpg";

            var newfile = "";

            var counter = 0;

            do
            {
                newfile = path + file + "." + counter + extension;
                counter++;

                if (counter > 999)
                    return;

            } while (File.Exists(newfile));
            
            try
            {
                CvSource.Save(newfile);
            }
            catch (Exception e)
            {
                counter = 0;

                do
                {
                    newfile = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + @"\" +file + "." + counter + extension;
                    counter++;

                    if (counter > 999)
                        return;

                } while (File.Exists(newfile));

                try
                {
                    CvSource.Save(newfile);
                }
                catch (Exception ex)
                {
                    LogManager.GetCurrentClassLogger().Error(e, "Can't save file");
                    return;
                }
            }

            LogManager.GetCurrentClassLogger().Info("File saved " + newfile);
        }
    }
}

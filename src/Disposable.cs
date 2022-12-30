////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;

namespace AvifFileType
{
    internal abstract class Disposable
        : IDisposable
    {
        private int isDisposed;

        protected Disposable()
        {
            this.isDisposed = 0;
        }

        ~Disposable()
        {
            if (Interlocked.Exchange(ref this.isDisposed, 1) == 0)
            {
                Dispose(disposing: false);
            }
        }

        public bool IsDisposed => Volatile.Read(ref this.isDisposed) == 1;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.isDisposed, 1) == 0)
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        ///   <see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.
        /// </param>
        protected abstract void Dispose(bool disposing);

        /// <summary>
        /// Verifies that the object has not been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        protected void VerifyNotDisposed()
        {
            if (this.IsDisposed)
            {
                ExceptionUtil.ThrowObjectDisposedException(GetType().Name);
            }
        }
    }
}

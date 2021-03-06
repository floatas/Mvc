﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Microsoft.AspNet.Mvc.Core;
using Microsoft.Framework.Internal;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNet.Mvc
{
    /// <summary>
    /// A <see cref="FileResult"/> on execution will write a file from disk to the response
    /// using mechanisms provided by the host.
    /// </summary>
    public class PhysicalFileProviderResult : FileResult
    {
        private const int DefaultBufferSize = 0x1000;
        private string _fileName;

        /// <summary>
        /// Creates a new <see cref="PhysicalFileProviderResult"/> instance with
        /// the provided <paramref name="fileName"/> and the provided <paramref name="contentType"/>.
        /// </summary>
        /// <param name="fileName">The path to the file. The path must be an absolute path.</param>
        /// <param name="contentType">The Content-Type header of the response.</param>
        public PhysicalFileProviderResult([NotNull] string fileName, [NotNull] string contentType)
            : this(fileName, new MediaTypeHeaderValue(contentType))
        {
        }

        /// <summary>
        /// Creates a new <see cref="PhysicalFileProviderResult"/> instance with
        /// the provided <paramref name="fileName"/> and the provided <paramref name="contentType"/>.
        /// </summary>
        /// <param name="fileName">The path to the file. The path must be an absolute path.</param>
        /// <param name="contentType">The Content-Type header of the response.</param>
        public PhysicalFileProviderResult([NotNull] string fileName, [NotNull] MediaTypeHeaderValue contentType)
            : base(contentType)
        {
            FileName = fileName;
        }

        /// <summary>
        /// Gets or sets the path to the file that will be sent back as the response.
        /// </summary>
        public string FileName
        {
            get
            {
                return _fileName;
            }

            [param: NotNull]
            set
            {
                _fileName = value;
            }
        }

        /// <inheritdoc />
        protected override async Task WriteFileAsync(HttpResponse response, CancellationToken cancellation)
        {
            if (!Path.IsPathRooted(FileName))
            {
                throw new FileNotFoundException(Resources.FormatFileResult_InvalidPath(FileName), FileName);
            }

            var sendFile = response.HttpContext.GetFeature<IHttpSendFileFeature>();
            if (sendFile != null)
            {
                await sendFile.SendFileAsync(
                    FileName,
                    offset: 0,
                    length: null,
                    cancellation: cancellation);

                return;
            }
            else
            {
                var fileStream = GetFileStream(FileName);

                using (fileStream)
                {
                    await fileStream.CopyToAsync(response.Body, DefaultBufferSize, cancellation);
                }

                return;
            }
        }

        /// <summary>
        /// Returns <see cref="Stream"/> for the specified <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The path for which the <see cref="FileStream"/> is needed.</param>
        /// <returns><see cref="FileStream"/> for the specified <paramref name="path"/>.</returns>
        protected virtual Stream GetFileStream([NotNull]string path)
        {
            return new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    DefaultBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
    }
}

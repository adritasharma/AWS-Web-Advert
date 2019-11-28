﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebAdvert.Web.Services
{
    public interface IS3FileUploader
    {
        Task<bool> UploadFileAsync(string fileName, Stream storageStream);
    }
}

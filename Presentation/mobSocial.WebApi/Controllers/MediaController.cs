﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Http;
using mobSocial.Core;
using mobSocial.Data.Constants;
using mobSocial.Data.Entity.MediaEntities;
using mobSocial.Data.Entity.Settings;
using mobSocial.Data.Enum;
using mobSocial.Data.Helpers;
using mobSocial.Services.MediaServices;
using mobSocial.Services.Social;
using mobSocial.Services.Users;
using mobSocial.WebApi.Configuration.Infrastructure;
using mobSocial.WebApi.Configuration.Mvc;
using mobSocial.WebApi.Extensions.ModelExtensions;

namespace mobSocial.WebApi.Controllers
{
    [RoutePrefix("media")]
    public class MediaController : RootApiController
    {
        private readonly MediaService _mediaService;
        private readonly MediaSettings _mediaSettings;
        private readonly IMobSocialVideoProcessor _videoProcessor;
        private readonly GeneralSettings _generalSettings;
        private readonly IUserService _userService;
        private readonly ICommentService _commentService;
        private readonly ILikeService _likeService;

        public MediaController(MediaService mediaService, MediaSettings mediaSettings, IMobSocialVideoProcessor videoProcessor, GeneralSettings generalSettings, IUserService userService, ICommentService commentService, ILikeService likeService)
        {
            _mediaService = mediaService;
            _mediaSettings = mediaSettings;
            _videoProcessor = videoProcessor;
            _generalSettings = generalSettings;
            _userService = userService;
            _commentService = commentService;
            _likeService = likeService;
        }

        [HttpGet]
        [Authorize]
        [Route("get/{id:int}")]
        public IHttpActionResult Get(int id)
        {
            var media = _mediaService.Get(id);
            if (media == null)
                return NotFound();
            //todo: verify permissions to see if media can be viewed by logged in user
            var model = media.ToModel(_mediaService, _mediaSettings, _generalSettings, _userService,
                commentService: _commentService,
                likeService: _likeService,
                withSocialInfo: true,
                withNextAndPreviousMedia: true);

            return RespondSuccess(new { Media = model });
        }

        [Authorize]
        [Route("uploadpictures")]
        [HttpPost]
        public IHttpActionResult UploadPictures()
        {
            var files = HttpContext.Current.Request.Files;
            if (files.Count == 0)
            {
                VerboseReporter.ReportError("No file uploaded", "upload_pictures");
                return RespondFailure();
            }
            var newImages = new List<object>();
            for (var index = 0; index < files.Count; index++)
            {

                //the file
                var file = files[index];

                //and it's name
                var fileName = file.FileName;
                //stream to read the bytes
                var stream = file.InputStream;
                var pictureBytes = new byte[stream.Length];
                stream.Read(pictureBytes, 0, pictureBytes.Length);

                //file extension and it's type
                var fileExtension = Path.GetExtension(fileName);
                if (!string.IsNullOrEmpty(fileExtension))
                    fileExtension = fileExtension.ToLowerInvariant();

                var contentType = file.ContentType;

                if (string.IsNullOrEmpty(contentType))
                {
                    contentType = PictureUtility.GetContentType(fileExtension);
                }

                var picture = new Media() {
                    Binary = pictureBytes,
                    MimeType = contentType,
                    Name = fileName,
                    UserId = ApplicationContext.Current.CurrentUser.Id,
                    DateCreated = DateTime.UtcNow
                };

                _mediaService.WritePictureBytes(picture, _mediaSettings.PictureSaveLocation);
                //save it
                _mediaService.Insert(picture);
                newImages.Add(new {
                    ImageUrl = _mediaService.GetPictureUrl(picture.Id),
                    ThumbnailUrl = _mediaService.GetPictureUrl(picture.Id, PictureSizeNames.ThumbnailImage),
                    ImageId = picture.Id,
                    MimeType = contentType
                });
            }

            return RespondSuccess(new { Images = newImages });
        }

        [Authorize]
        [Route("uploadvideo")]
        [HttpPost]
        public IHttpActionResult UploadVideo()
        {
            var files = HttpContext.Current.Request.Files;
            if (files.Count == 0)
            {
                VerboseReporter.ReportError("No file uploaded", "upload_videos");
                return RespondFailure();
            }

            var file = files[0];
            //and it's name
            var fileName = file.FileName;


            //file extension and it's type
            var fileExtension = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(fileExtension))
                fileExtension = fileExtension.ToLowerInvariant();

            var contentType = file.ContentType;

            if (string.IsNullOrEmpty(contentType))
            {
                contentType = VideoUtility.GetContentType(fileExtension);
            }

            if (contentType == string.Empty)
            {
                VerboseReporter.ReportError("Invalid file type", "upload_videos");
                return RespondFailure();
            }
            
            var bytes = new byte[file.ContentLength];
            file.InputStream.Read(bytes, 0, bytes.Length);

            //create a new media
            var media = new Media()
            {
                MediaType = MediaType.Video,
                Binary = bytes,
                MimeType = contentType,
                Name = fileName,
                UserId = ApplicationContext.Current.CurrentUser.Id,
                DateCreated = DateTime.UtcNow
            };

            _mediaService.WriteVideoBytes(media);
            //insert now
            _mediaService.Insert(media);
           return RespondSuccess(new {
                VideoId = media.Id,
                VideoUrl = WebHelper.GetUrlFromPath(media.LocalPath, _generalSettings.VideoServerDomain),
                ThumbnailUrl = WebHelper.GetUrlFromPath(media.ThumbnailPath, _generalSettings.ImageServerDomain),
                MimeType = file.ContentType
            });
        }
    }
}
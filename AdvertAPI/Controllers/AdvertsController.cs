﻿using AdvertAPI.Models;
using AdvertAPI.Models.Messages;
using AdvertAPI.Services;
using Amazon.SimpleNotificationService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdvertAPI.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class AdvertsController : ControllerBase
    {
        private readonly IAdvertStorageService _advertStorageService;
        private readonly ILogger<AdvertsController> _logger;

        public IConfiguration Configuration { get; }

        public AdvertsController(IAdvertStorageService advertStorageService, IConfiguration configuration, ILogger<AdvertsController> logger)
        {
            _advertStorageService = advertStorageService;
            Configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        [Route("Create")]
        [ProducesResponseType(400)]
        [ProducesResponseType(200, Type = typeof(CreateAdvertResponse))]
        public async Task<IActionResult> CreateAsync(AdvertModel model)
        {
            string recordId = string.Empty;
            try
            {
                recordId = await _advertStorageService.Add(model);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateAsync Method");
                return StatusCode(500, ex.Message);
            }

            return Ok(new CreateAdvertResponse() { Id = recordId });
        }

        [HttpPut]
        [Route("Confirm")]
        [ProducesResponseType(404)]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Confirm(ConfirmAdvertModel model)
        {
            try
            {
                await _advertStorageService.Confirm(model);
                await RaiseAdvertConfirmedMessage(model);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogError(ex, "Confirm Method");
                return new NotFoundResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Confirm Method");
                return StatusCode(500, ex.Message);
            }
            return Ok();
        }

        private async Task RaiseAdvertConfirmedMessage(ConfirmAdvertModel model)
        {
            var topicArn = Configuration.GetValue<string>("TopicArn");
            var dbModel = await _advertStorageService.GetById(model.Id);

            using (var client = new AmazonSimpleNotificationServiceClient())
            {
                var message = new AdvertConfirmedMessage()
                {
                    Id = model.Id,
                    Title = dbModel.Title
                };

                var messageJson = JsonConvert.SerializeObject(message);
                await client.PublishAsync(topicArn, messageJson);
            }
        }
    }
}

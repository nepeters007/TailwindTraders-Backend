﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using Tailwind.Traders.Profile.Api.Csv;
using Tailwind.Traders.Profile.Api.Helpers;
using Tailwind.Traders.Profile.Api.Models;

namespace Tailwind.Traders.Profile.Api.Infrastructure
{
    public class ProfileContextSeed
    {
        private readonly CsvReaderHelper _csvHelper;
        private readonly ILogger _logger;

        public ProfileContextSeed(CsvReaderHelper csvHelper, ILogger logger)
        {
            _csvHelper = csvHelper;
            _logger = logger;
        }

        public async Task SeedAsync(ProfileDbContext profileContext, IWebHostEnvironment env)
        {
            var contentRootPath = env.ContentRootPath;

            if (!profileContext.Profiles.Any())
            {
                var records = _csvHelper.LoadCsv<ProfileData>(contentRootPath, "Profiles");
                var profiles = records.Select(r => new Profiles()
                {
                    Id = r.Id,
                    Address = r.Address,
                    Email = r.Email,
                    Name = r.Name,
                    PhoneNumber = r.PhoneNumber,
                    ImageNameSmall = r.ImageNameSmall,
                    ImageNameMedium = r.ImageNameMedium
                });
                await profileContext.Profiles.AddRangeAsync(profiles);
                await profileContext.SaveChangesAsync();
            }
        }        
    }    
}

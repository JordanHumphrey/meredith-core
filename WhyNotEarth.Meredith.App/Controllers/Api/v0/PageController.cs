namespace WhyNotEarth.Meredith.App.Controllers.Api.v0
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Cors;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Newtonsoft.Json;
    using WhyNotEarth.Meredith.App.Models.Api.V0.Page;
    using WhyNotEarth.Meredith.Data.Entity;
    using WhyNotEarth.Meredith.Data.Entity.Models;
    using WhyNotEarth.Meredith.Pages;

    [ApiVersion("0")]
    [Route("/api/v0/pages")]
    [EnableCors]
    public class PageController : Controller
    {
        private MeredithDbContext MeredithDbContext { get; }

        private StoryService StoryService { get; }

        private IQueryable<Page> PageIncludes() => MeredithDbContext.Pages
            .Include(p => p.Company)
            .Include(p => p.Cards)
            .Include(p => p.Category)
            .Include(p => p.Hotel)
            .ThenInclude(p => p.Amenities)
            .Include(p => p.Hotel)
            .ThenInclude(p => p.RoomTypes)
            .Include(p => p.Hotel)
            .ThenInclude(p => p.RoomTypes)
            .ThenInclude(p => p.Beds)
            .Include(p => p.Hotel)
            .ThenInclude(p => p.Rules)
            .Include(p => p.Hotel)
            .ThenInclude(p => p.Spaces)
            .Include(p => p.Images);

        public PageController(
            StoryService storyService,
            MeredithDbContext meredithDbContext)
        {
            StoryService = storyService;
            MeredithDbContext = meredithDbContext;
        }

        [HttpGet]
        [Route("slug/{companySlug}/{pageSlug}")]
        public async Task<IActionResult> Get(string companySlug, string pageSlug)
        {
            var page = await PageIncludes()
                .FirstOrDefaultAsync(p =>
                    p.Company.Slug.ToLower() == companySlug.ToLower()
                    && p.Slug.ToLower() == pageSlug.ToLower());
            if (page == null)
            {
                return NotFound();
            }

            return Ok(new[] { page }.AsQueryable().Select(p => PageToReturn(p, StoryService)).FirstOrDefault());
        }

        [HttpGet]
        [Route("slug/{companySlug}")]
        public async Task<IActionResult> GetPages(string companySlug)
        {
            var pages = await PageIncludes()
                .Where(p =>
                    p.Company.Slug.ToLower() == companySlug.ToLower())
                .ToListAsync();
            if (pages.Count == 0)
            {
                return NotFound();
            }

            return Ok(pages.AsQueryable().Select(p => PageToReturn(p, StoryService)).ToList());
        }

        [HttpGet]
        [Route("slug/{companySlug}/categories/by-name/{categoryName}")]
        public async Task<IActionResult> ByCompanyByCategoryName(string companySlug, string categoryName)
        {
            var pages = await PageIncludes()
                .Where(p => p.Company.Slug == companySlug
                    && p.Category.Name == categoryName)
                .ToListAsync();
            return Ok(pages.AsQueryable().Select(p => PageToReturn(p, StoryService)).ToList());
        }

        private readonly Func<Page, StoryService, PageModel> PageToReturn = (page, storyService) =>
        {
            var pageModel = new PageModel
            {
                Id = page.Id,
                Brand = page.Company.Slug,
                Name = page.Name,
                Title = page.Title,
                Description = page.Description,
                H2 = page.Header,
                Slug = page.Slug,
                BackgroundImage = page.BackgroundImage,
                FeaturedImage = page.FeaturedImage,
                Images = page.Images
                    .OrderBy(i => i.Order)
                    .Select(i => new Models.Api.V0.Page.Image
                    {
                        Order = i.Order,
                        Url = i.Url
                    }).ToList(),
                CtaText = page.CallToAction,
                CtaLink = page.CallToActionLink,
                Stories = page.Cards
                    .OrderBy(c => c.Order)
                    .Select(c => new Story
                    {
                        Content = c.Text,
                        CtaText = c.CallToAction,
                        CtaLink = c.CallToActionUrl,
                        Id = c.Id,
                        Image = c.BackgroundUrl,
                        PosterUrl = c.PosterUrl,
                        Blur = "2px",
                        Type = storyService.GetCardType(c.CardType)
                    })
                    .ToList(),
                Custom = page.Custom == null ? null : JsonConvert.DeserializeObject<dynamic>(page.Custom),
            };

            if (page.Category != null)
            {
                pageModel.Categories.Add(
                    new Models.Api.V0.Page.Category
                    {
                        Name = page.Category.Name,
                        Id = page.Category.Id
                    });
            }

            if (page.Hotel != null)
            {
                pageModel.Modules.Add("hotel", new
                {
                    Amenities = page.Hotel.Amenities.Select(a => a.Text).ToList(),
                    page.Hotel?.Id,
                    page.Hotel?.GettingAround,
                    page.Hotel?.Location,
                    RoomTypes = page.Hotel?.RoomTypes.Select(r => new
                    {
                        r.Capacity,
                        Beds = r.Beds.Select(b => new
                        {
                            b.Count,
                            Id = (int)b.BedType,
                            Type = b.BedType.ToString()
                        }).ToList(),
                        r.Name,
                        r.Id
                    }).ToList(),
                    Rules = page.Hotel?.Rules.Select(r => r.Text).ToList(),
                    Spaces = page.Hotel?.Spaces.Select(s => s.Name).ToList()
                });
            }

            return pageModel;
        };
    }
}
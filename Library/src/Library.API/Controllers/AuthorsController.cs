using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Library.API.Models;
using Library.API.Services;
using Microsoft.AspNetCore.Mvc;
using Library.API.Helpers;
using AutoMapper;
using Library.API.Entities;
using Microsoft.AspNetCore.Http;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Library.API.Controllers
{
    [Route("api/[controller]")]
    public class AuthorsController : Controller
    {
        private ILibraryRepository _libraryRepository;
        private IUrlHelper _urlHelper;
        private IPropertyMappingService _propertyMappingService;
        private ITypeHelperService _typeHelperService;

        public AuthorsController(ILibraryRepository libraryRepository, 
            IUrlHelper urlHelper, 
            IPropertyMappingService propertyMappingService,
            ITypeHelperService typeHelperService)
        {
            _libraryRepository = libraryRepository;
            _urlHelper = urlHelper;
            _propertyMappingService = propertyMappingService;
            _typeHelperService = typeHelperService;
        }
        [HttpGet(Name ="GetAuthors")]
        // 12 add support for HEAD verb by just adding the HEAD attribute
        [HttpHead]
        // 10 add custom vendor specific media from header
        public IActionResult GetAuthors(AuthorsResourceParameters authorsResourceParameters, [FromHeader(Name = "Accept")] string mediaType)
        {
            if(!_propertyMappingService.ValidMappingExistsFor<AuthorDto, Author>(authorsResourceParameters.OrderBy))
            {
                return BadRequest();
            }
            if(!_typeHelperService.TypeHasProperties<AuthorDto>(authorsResourceParameters.Fields))
            {
                return BadRequest();
            }
            var authorsFromRepo = _libraryRepository.GetAuthors(authorsResourceParameters);
           
            var authors = Mapper.Map<IEnumerable<AuthorDto>>(authorsFromRepo);
            // 10 this code only executes if this vendor media type is specified
            if (mediaType == "application/vnd.marvin.hateoas+json")
            {
                //09 Code is removed because these link drive app state, so they can be created in the CreateLinksForAuthors()
                //var previousPageLink = authorsFromRepo.HasPrevious ? CreateAuthorsResourceUri(authorsResourceParameters, ResourceUriType.PreviousPage) : null;
                //var nextPageLink = authorsFromRepo.HasNext ? CreateAuthorsResourceUri(authorsResourceParameters, ResourceUriType.NextPage) : null;
                var paginationMetadata = new
                {
                    totalCount = authorsFromRepo.TotalCount,
                    pageSize = authorsFromRepo.PageSize,
                    currentPage = authorsFromRepo.CurrentPage,
                    totalPages = authorsFromRepo.TotalPages,
                    //09 Code is removed because these link drive app state, so they can be created in the CreateLinksForAuthors()
                    //previousPageLink = previousPageLink,
                    //nextPageLink = nextPageLink
                };
                Response.Headers.Add("X-Pagination", Newtonsoft.Json.JsonConvert.SerializeObject(paginationMetadata));
                //09 create links for authors collection
                var links = CreateLinksForAuthors(authorsResourceParameters, authorsFromRepo.HasNext, authorsFromRepo.HasPrevious);
                var shapedAuthors = authors.ShapeData(authorsResourceParameters.Fields);
                //09 add links property to each author in collection
                var shapedAuthorsWithLinks = shapedAuthors.Select(author =>
                {
                //09 cast each expando object to dictionary
                var authorAsDictionary = author as IDictionary<string, object>;
                //09 create link for each author
                var authorLinks = CreateLinksForAuthor(
                        (Guid)authorAsDictionary["Id"], authorsResourceParameters.Fields);
                //09 add link to author
                authorAsDictionary.Add("links", authorLinks);
                    return authorAsDictionary;
                });
                //09 create and return anonymous object
                var linkedCollectionResource = new
                {
                    value = shapedAuthorsWithLinks,
                    links = links
                };

                return Ok(linkedCollectionResource);
            }
            else
            {
                // 10 code re-added from before implementing the vendor hateos code
                var previousPageLink = authorsFromRepo.HasPrevious ? CreateAuthorsResourceUri(authorsResourceParameters, ResourceUriType.PreviousPage) : null;
                var nextPageLink = authorsFromRepo.HasNext ? CreateAuthorsResourceUri(authorsResourceParameters, ResourceUriType.NextPage) : null;
                var paginationMetadata = new
                {
                    previousPageLink = previousPageLink,
                    nextPageLink = nextPageLink,
                    totalCount = authorsFromRepo.TotalCount,
                    pageSize = authorsFromRepo.PageSize,
                    currentPage = authorsFromRepo.CurrentPage,
                    totalPages = authorsFromRepo.TotalPages
                    
                };
                Response.Headers.Add("X-Pagination", Newtonsoft.Json.JsonConvert.SerializeObject(paginationMetadata));
            
            //09 change from authors.ShapeData(authorsResourceParameters.Fields) to linkedCollectionResource 
            // 10 revert the previous change, change from linkedCollectionResourcet to ShapeData
            //return Ok(linkedCollectionResource);
            return Ok(authors.ShapeData(authorsResourceParameters.Fields));
			}
        }
        private string CreateAuthorsResourceUri(AuthorsResourceParameters authorsResourceParameters, ResourceUriType type)
        {
            switch (type)
            {
                case ResourceUriType.PreviousPage:
                    return _urlHelper.Link("GetAuthors",
                        new
                        {
                            fields = authorsResourceParameters.Fields,
                            orderBy = authorsResourceParameters.OrderBy,
                            searchQuery = authorsResourceParameters.SearchQuery,
                            genre = authorsResourceParameters.Genre,
                            pageNumber = authorsResourceParameters.PageNumber - 1,
                            pageSize = authorsResourceParameters.PageSize
                        });
                case ResourceUriType.NextPage:
                    return _urlHelper.Link("GetAuthors",
                        new
                        {
                            fields = authorsResourceParameters.Fields,
                            orderBy = authorsResourceParameters.OrderBy,
                            searchQuery = authorsResourceParameters.SearchQuery,
                            genre = authorsResourceParameters.Genre,
                            pageNumber = authorsResourceParameters.PageNumber + 1,
                            pageSize = authorsResourceParameters.PageSize
                        });
                //09 Add current 
                case ResourceUriType.Current:
                default:
                    return _urlHelper.Link("GetAuthors",
                        new
                        {
                            fields = authorsResourceParameters.Fields,
                            orderBy = authorsResourceParameters.OrderBy,
                            searchQuery = authorsResourceParameters.SearchQuery,
                            genre = authorsResourceParameters.Genre,
                            pageNumber = authorsResourceParameters.PageNumber,
                            pageSize = authorsResourceParameters.PageSize
                        });
            }
        }
        [HttpGet("{id}", Name = "GetAuthor")]
        public IActionResult GetAuthor(Guid id, [FromQuery] string fields)
        {
            if (!_typeHelperService.TypeHasProperties<AuthorDto>(fields))
            {
                return BadRequest();
            }
            var authorFromRepo = _libraryRepository.GetAuthor(id);

            if (authorFromRepo == null)
            {
                return NotFound();
            }
            var author = Mapper.Map<AuthorDto>(authorFromRepo);
            // 09 create property of links to the response body, pass in method params
            var links = CreateLinksForAuthor(id, fields);
            // 09 cast expando object to dictionary type
            var linkedResourceToReturn = author.ShapeData(fields) as IDictionary<string, object>;
            // 09 add new links field to dictionary type
            linkedResourceToReturn.Add("links", links);
            return Ok(linkedResourceToReturn);
        }
        [HttpPost(Name ="CreateAuthor")]
        [RequestHeaderMatchesMediaType("Content-Type", new[] { "application/vnd.marvin.author.full+json"})]
        public IActionResult CreateAuthor([FromBody] AuthorCreationDto author)
        {
            if (author == null)
            {
                return BadRequest();
            }
            var authorEntity = Mapper.Map<Author>(author);
            _libraryRepository.AddAuthor(authorEntity);
            if (!_libraryRepository.Save())
            {
                throw new Exception("Creating an author failed on save.");
                //return StatusCode(500, "A problem happened with handling your request.");
            }
            var authorToReturn = Mapper.Map<AuthorDto>(authorEntity);
            // 09 create the links for the author, no fields param on POST
            var links = CreateLinksForAuthor(authorToReturn.Id, null);
            // 09 cast Expando Object to dictionary
            var linkedResourceToReturn = authorToReturn.ShapeData(null) as IDictionary<string, object>;
            // 09 add Links to dictionary
            linkedResourceToReturn.Add("links", links);
            // 09 update to return linkedResourceToReturn instead of authorToReturn
            // 09 update to pass in the id from linkedResourceToReturn instead of id from authorToReturn
            return CreatedAtRoute("GetAuthor", new { id=linkedResourceToReturn["Id"]}, linkedResourceToReturn);

        }

        // 10 Copy from CreateAthor method and modify to accept date of death
        [HttpPost(Name = "CreateAuthorWithDateOfDeath")]
        // 10 Add custom media type attribute
        [RequestHeaderMatchesMediaType("Content-Type", new[] { "application/vnd.marvin.authorwithdateofdeath.full+json",
        "application/vnd.marvin.authorwithdateofdeath.full+xml"})]
        // 10 showing to have multiple instances of the same restraint (add AllowMultiple in custom constraint)
        //[RequestHeaderMatchesMediaType("Accept", new[] { "..." })]
        public IActionResult CreateAuthorWithDateOfDeath([FromBody] AuthorForCreationWithDateOfDeathDto author)
        {
            if (author == null)
            {
                return BadRequest();
            }
            var authorEntity = Mapper.Map<Author>(author);

            _libraryRepository.AddAuthor(authorEntity);

            if (!_libraryRepository.Save())
            {
                throw new Exception("Creating an author failed on save.");
                //return StatusCode(500, "A problem happened with handling your request.");
            }
            var authorToReturn = Mapper.Map<AuthorDto>(authorEntity);
            // 09 create the links for the author, no fields param on POST
            var links = CreateLinksForAuthor(authorToReturn.Id, null);
            // 09 cast Expando Object to dictionary
            var linkedResourceToReturn = authorToReturn.ShapeData(null) as IDictionary<string, object>;
            // 09 add Links to dictionary
            linkedResourceToReturn.Add("links", links);
            // 09 update to return linkedResourceToReturn instead of authorToReturn
            // 09 update to pass in the id from linkedResourceToReturn instead of id from authorToReturn
            return CreatedAtRoute("GetAuthor", new { id = linkedResourceToReturn["Id"] }, linkedResourceToReturn);

        }


        [HttpPost("{id}")]
        public IActionResult BlockAuthorCreation(Guid id)
        {
            if (_libraryRepository.AuthorExists(id))
            {
                return new StatusCodeResult(StatusCodes.Status409Conflict);
            }
            return NotFound();
        }

        [HttpDelete("{id}", Name = "DeleteAuthor")]
        public IActionResult DeleteAuthor(Guid id)
        {
            var authorFromRepo = _libraryRepository.GetAuthor(id);
            if(authorFromRepo == null)
            {
                return NotFound();
            }
            _libraryRepository.DeleteAuthor(authorFromRepo);
            if (!_libraryRepository.Save())
            {
                throw new Exception($"Deleting author {id} failed on save.");
            }
            return NoContent();
        }

        /// <summary>
        /// 09 Creates links property for Author; 
        /// </summary>
        /// <param name="id">URI params needed to create response</param>
        /// <param name="fields">URI params needed to create response, datashaping custom fields to be returned</param>
        /// <returns>IEnumerable of LinkDto type</returns>
        private IEnumerable<LinkDto> CreateLinksForAuthor(Guid id, string fields)
        {
            var links = new List<LinkDto>();
            if(string.IsNullOrWhiteSpace(fields))
            {
                links.Add(
                // 09 use urlHelper to construct URI
                // 09 href
                  new LinkDto(_urlHelper.Link("GetAuthor", new { id = id }),
                  // 09 rel
                  "self",
                  // 09 method
                  "GET"));
            }
            else
            {
                links.Add(
                  // 09 include custom fields desired
                  new LinkDto(_urlHelper.Link("GetAuthor", new { id = id, fields = fields }),
                  "self",
                  "GET"));
            }
            // 09 decide what the client should be able to do with an author that is returned based on business logic
            // 09 delete, create book author, get books for author, we're driving the client actions
            links.Add(
              new LinkDto(_urlHelper.Link("DeleteAuthor", new { id = id }),
              "delete_author",
              "DELETE"));

            links.Add(
              new LinkDto(_urlHelper.Link("CreateBookForAuthor", new { authorId = id }),
              "create_book_for_author",
              "POST"));

            links.Add(
               new LinkDto(_urlHelper.Link("GetBooksForAuthor", new { authorId = id }),
               "books",
               "GET"));
            return links;
        }
        // 09 private method to generate links properties for AuthorResourceParameters type from GetAuthors() arguement
        private IEnumerable<LinkDto> CreateLinksForAuthors(AuthorsResourceParameters authorsResourceParameters,
            bool hasNext, bool hasPrevious)
        {
            var links = new List<LinkDto>();

            // self 
            links.Add(
               //09 instead of using the UrlHelper this time, the CreateAuthorsResourceUri from pagination is used
               new LinkDto(CreateAuthorsResourceUri(authorsResourceParameters,
               ResourceUriType.Current)
               , "self", "GET"));

            if (hasNext)
            {
                links.Add(
                  new LinkDto(CreateAuthorsResourceUri(authorsResourceParameters,
                  ResourceUriType.NextPage),
                  "nextPage", "GET"));
            }

            if (hasPrevious)
            {
                links.Add(
                    new LinkDto(CreateAuthorsResourceUri(authorsResourceParameters,
                    ResourceUriType.PreviousPage),
                    "previousPage", "GET"));
            }

            return links;
        }
        // 12 support the OPTIONS verb
        [HttpOptions]
        public IActionResult GetAuthorsOptions()
        {
            Response.Headers.Add("Allow", "GET,OPTIONS,POST");
            return Ok();
        }
    }
}

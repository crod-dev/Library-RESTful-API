using AutoMapper;
using Library.API.Entities;
using Library.API.Models;
using Library.API.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Library.API.Controllers
{
    [Route("api/authors/{authorId}/books")]
    public class BooksController : Controller
    {
        private ILibraryRepository _libraryRepository;
        private ILogger _logger;
        private IUrlHelper _urlHelper;

        // 09 Inject UrlHelper to have the ability to create links
        public BooksController(ILibraryRepository libraryRepository, ILogger<BooksController> logger, IUrlHelper urlHelper)
        {
            _libraryRepository = libraryRepository;
            _logger = logger;
            _urlHelper = urlHelper;
        }

        // 09 Add names to action methods to access them with the UrlHelper to create the URI
        [HttpGet(Name = "GetBooksForAuthor")]
        public IActionResult GetBooksForAuthor(Guid authorId)
        {
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var booksForAuthorFromRepo = _libraryRepository.GetBooksForAuthor(authorId);
            var booksForAuthor = Mapper.Map<IEnumerable<BookDto>>(booksForAuthorFromRepo);

            // 09 create links for each book in the collection by passing it into CreateLinksForBook () before returning
            booksForAuthor = booksForAuthor.Select(book =>
            {
                book = CreateLinksForBook(book);
                return book;
            });
            // 09 Create wrapper for collection of books
            var wrapper = new LinkedCollectionResourceWrapperDto<BookDto>(booksForAuthor);
            // 09 Create the links on the wrapper before returning
            return Ok(CreateLinksForBooks(wrapper));
        }
        [HttpGet("{id}", Name = "GetBookForAuthor")]
        public IActionResult GetBookForAuthor(Guid authorId, Guid id)
        {
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }
            var bookForAuthoFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if (bookForAuthoFromRepo == null)
            {
                return NotFound();
            }
            var bookForAuthor = Mapper.Map<BookDto>(bookForAuthoFromRepo);
            // 09 create links for book by passing it into CreateLinksForBook () before returning
            return Ok(CreateLinksForBook(bookForAuthor));
        }
        // 09 Give name to be referenced from AuthorsController CreateLinkForAuthor() 
        [HttpPost(Name ="CreateBookForAuthor")]
        public IActionResult CreateBookForAuthor(Guid authorId, [FromBody] BookCreationDto book)
        {
            if (book == null)
            {
                return BadRequest();
            }
            if(book.Description == book.Title)
            {
                ModelState.AddModelError(nameof(BookCreationDto), "The provided description should be different fromt the title.");
            }
            if (!ModelState.IsValid)
            {
                //return 422
                return new UnprocessableEntityObjectResult(ModelState);
            }
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }
            var bookEntity = Mapper.Map<Book>(book);
            _libraryRepository.AddBookForAuthor(authorId, bookEntity);
            if (!_libraryRepository.Save())
            {
                throw new Exception($"Creating a book for author {authorId} failed to save.");
            }
            var bookToReturn = Mapper.Map<BookDto>(bookEntity);
            return CreatedAtRoute("GetBookForAuthor", new { authorId = authorId, id = bookToReturn.Id },
                // 09 create links for book by passing it into CreateLinksForBook () before returning
                CreateLinksForBook(bookToReturn));
        }

        // 09 Add names to action methods to access them with the UrlHelper to create the URI
        [HttpDelete("{id}", Name = "DeleteBookForAuthor")]
        public IActionResult DeleteBookForAuthor(Guid authorId, Guid id)
        {
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }
            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if (bookForAuthorFromRepo == null)
            {
                return NotFound();
            }
            _libraryRepository.DeleteBook(bookForAuthorFromRepo);
            if (!_libraryRepository.Save())
            {
                throw new Exception($"Deleting book {id} for author {authorId} failed on save.");
            }
            _logger.LogInformation(100, $"Book {id} for author {authorId} was deleted.");
            return NoContent();
        }

        // 09 Add names to action methods to access them with the UrlHelper to create the URI
        [HttpPut("{id}", Name = "UpdateBookForAuthor")]
        public IActionResult UpdateBookForAuthor(Guid authorId, Guid id, [FromBody] BookUpdateDto book)
        {
            if (book == null)
            {
                return NotFound();
            }
            if (book.Description == book.Title)
            {
                ModelState.AddModelError(nameof(BookUpdateDto), "The provided description should be different from the title.");
            }
            if (!ModelState.IsValid)
            {
                return new UnprocessableEntityObjectResult(ModelState);
            }
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }
            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if (bookForAuthorFromRepo == null)
            {
                var bookToAdd = Mapper.Map<Book>(book);
                bookToAdd.Id = id;
                _libraryRepository.AddBookForAuthor(authorId, bookToAdd);
                if (!_libraryRepository.Save())
                {
                    throw new Exception($"Upserting book {id} for author {authorId} failed to save.");
                }
                var bookToReturn = Mapper.Map<BookDto>(bookToAdd);
                return CreatedAtRoute("GetBookForAuthor",
                    new { authorId = authorId, id = bookToReturn.Id },
                    bookToReturn);
            }
            Mapper.Map(book, bookForAuthorFromRepo);
            _libraryRepository.UpdateBookForAuthor(bookForAuthorFromRepo);
            if (!_libraryRepository.Save())
            {
                throw new Exception($"Updating book {id} for author {authorId} failed to save.");
            }
            return NoContent();
        }

        // 09 Add names to action methods to access them with the UrlHelper to create the URI
        [HttpPatch("{id}", Name = "PartiallyUpdateBookForAuthor")]
        public IActionResult PartiallyUpdateBookForAuthor(Guid authorId, Guid id, [FromBody] JsonPatchDocument<BookUpdateDto> patchDoc)
        {
            if(patchDoc == null)
            {
                return BadRequest();
            }
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }
            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);
            if(bookForAuthorFromRepo == null)
            {
                var bookDto = new BookUpdateDto();
                patchDoc.ApplyTo(bookDto, ModelState);
                if(bookDto.Description == bookDto.Title)
                {
                    ModelState.AddModelError(nameof(BookUpdateDto),
                        "The provided description should be different from the title.");
                }
                TryValidateModel(bookDto);
                if(!ModelState.IsValid)
                {
                    return new UnprocessableEntityObjectResult(ModelState);
                }
                var bookToAdd = Mapper.Map<Book>(bookDto);
                bookToAdd.Id = id;
                _libraryRepository.AddBookForAuthor(authorId, bookToAdd);
                if (!_libraryRepository.Save())
                {
                    throw new Exception($"Upserting book {id} for author {authorId} failed to save.");
                }
                var bookToReturn = Mapper.Map<BookDto>(bookToAdd);
                return CreatedAtRoute("GetBookForAuthor", new { authorId = authorId, id = bookToReturn.Id }, bookToReturn);

            }
            var bookToPatch = Mapper.Map<BookUpdateDto>(bookForAuthorFromRepo);
            //patchDoc.ApplyTo(bookToPatch, ModelState);
            patchDoc.ApplyTo(bookToPatch);
            if(bookToPatch.Description == bookToPatch.Title)
            {
                ModelState.AddModelError(nameof(BookUpdateDto),
                    "The provided description should be different from the title.");
            }
            TryValidateModel(bookToPatch);
            if (!ModelState.IsValid)
            {
                return new UnprocessableEntityObjectResult(ModelState);
            }
            Mapper.Map(bookToPatch, bookForAuthorFromRepo);
            _libraryRepository.UpdateBookForAuthor(bookForAuthorFromRepo);
            if (!_libraryRepository.Save())
            {
                throw new Exception($"Patching book {id} for author {authorId} failed to save.");
            }
            return NoContent();
        }

        // 09 private method that will create the links for the BookDto (returning data model)
        private BookDto CreateLinksForBook(BookDto book)
        {
            // 09 here it is decided which links are return based on business logic
            // 09 first should alway be a link to itself
            // 09 book now has Links property since it now inherits the LinkedResourceBaseDto class
            // 09 UrlHelper is used to create the link (first param) for the LinkDto oject 
            // 09 href value
            book.Links.Add(new LinkDto(_urlHelper.Link("GetBookForAuthor",
                // 09 anonymous objec with bookId (GUID param in action method)
                new { id = book.Id }),
                // 09 rel value
                "self",
                // 09 method value
                "GET"));
            book.Links.Add(new LinkDto(_urlHelper.Link("DeleteBookForAuthor",
                new { id = book.Id }),
                "self",
                "DELETE"));
            book.Links.Add(new LinkDto(_urlHelper.Link("UpdateBookForAuthor",
                new { id = book.Id }),
                "self",
                "PUT"));
            book.Links.Add(new LinkDto(_urlHelper.Link("PartiallyUpdateBookForAuthor",
                new { id = book.Id }),
                "self",
                "PATCH"));

            return book;
        }

        // 09 private method populates the links for our new collections wrapper class LinkedCollectoinResourceWrapperDto
        // 09 returns itself
        private LinkedCollectionResourceWrapperDto<BookDto> CreateLinksForBooks(LinkedCollectionResourceWrapperDto<BookDto> booksWrapper)
        {
            //link to itself
            booksWrapper.Links.Add(
                new LinkDto(_urlHelper.Link("GetBooksForAuthor", new { }),
                "self",
                "GET"));

            return booksWrapper;
        }
    }
}

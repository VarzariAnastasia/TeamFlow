using TeamFlow.Domain.Entities;
using TeamFlow.Domain.Exceptions;

namespace TeamFlow.Domain.UnitTests.Entities
{
   public class TaskCommentTests
   {
      private static readonly DateTime Now = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

      private static readonly Guid TaskId = Guid.NewGuid();
      private static readonly Guid AuthorId = Guid.NewGuid();

      private static TaskComment CreateComment(string content = "Looks good")
          => new(TaskId, AuthorId, content, Now);

      [Fact]
      public void Constructor_WithValidData_SetsFields()
      {
         var comment = CreateComment();

         Assert.NotEqual(Guid.Empty, comment.Id);
         Assert.Equal(TaskId, comment.TaskId);
         Assert.Equal(AuthorId, comment.UserId);
         Assert.Equal("Looks good", comment.Content);
         Assert.Equal(Now, comment.CreatedAt);
         Assert.Null(comment.UpdatedAt);
      }

      [Theory]
      [InlineData("")]
      [InlineData("   ")]
      [InlineData(null)]
      public void Constructor_WithEmptyContent_Throws(string? content)   // BR-C1
      {
         Assert.Throws<BusinessRuleException>(() => CreateComment(content!));
      }

      [Fact]
      public void Constructor_WithContentOf2000Chars_Succeeds()   // BR-C1 boundary
      {
         var content = new string('a', 2000);

         var comment = CreateComment(content);

         Assert.Equal(content, comment.Content);
      }

      [Fact]
      public void Constructor_WithContentOf2001Chars_Throws()   // BR-C1
      {
         Assert.Throws<BusinessRuleException>(() => CreateComment(new string('a', 2001)));
      }

      [Fact]
      public void Constructor_TrimsContent()   // BR-C1
      {
         var comment = CreateComment("  Looks good  ");

         Assert.Equal("Looks good", comment.Content);
      }

      [Fact]
      public void UpdateContent_SetsContentAndUpdatedAt()   // BR-C2
      {
         var comment = CreateComment();
         var later = Now.AddHours(1);

         comment.UpdateContent("  Updated text  ", later);

         Assert.Equal("Updated text", comment.Content);
         Assert.Equal(later, comment.UpdatedAt);
         Assert.Equal(Now, comment.CreatedAt);
      }

      [Fact]
      public void UpdateContent_WithEmptyContent_ThrowsAndKeepsOriginal()   // BR-C1
      {
         var comment = CreateComment();

         Assert.Throws<BusinessRuleException>(() => comment.UpdateContent("   ", Now.AddHours(1)));
         Assert.Equal("Looks good", comment.Content);
         Assert.Null(comment.UpdatedAt);
      }
   }
}

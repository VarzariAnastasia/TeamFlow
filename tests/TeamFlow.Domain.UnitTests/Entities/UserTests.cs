using TeamFlow.Domain.Entities;
using TeamFlow.Domain.Enums;
using TeamFlow.Domain.Exceptions;

namespace TeamFlow.Domain.UnitTests.Entities
{
   public class UserTests
   {
      private static readonly DateTime Now = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

      private static User CreateUser(string name = "Ana Popescu", string email = "ana@example.com", UserRole role = UserRole.Employee)
          => new(name, email, role, Now);

     
      [Fact]
      public void Constructor_WithValidData_IsActiveAndSetsCreatedAt()   // BR-U1
      {
         var user = CreateUser();

         Assert.NotEqual(Guid.Empty, user.Id);
         Assert.True(user.IsActive);
         Assert.Equal(Now, user.CreatedAt);
         Assert.Equal(UserRole.Employee, user.Role);
      }

      [Theory]
      [InlineData("")]
      [InlineData("   ")]
      [InlineData(null)]
      public void Constructor_WithEmptyName_Throws(string? name)
      {
         Assert.Throws<BusinessRuleException>(() => CreateUser(name: name!));
      }

      [Fact]
      public void Constructor_WithNameOf201Chars_Throws()  
      {
         Assert.Throws<BusinessRuleException>(() => CreateUser(name: new string('a', 201)));
      }

      [Fact]
      public void Constructor_TrimsName()
      {
         var user = CreateUser(name: "  Ana Popescu  ");

         Assert.Equal("Ana Popescu", user.Name);
      }

      [Fact]
      public void Constructor_NormalizesEmailToTrimmedLowercase()   // BR-U2
      {
         var user = CreateUser(email: "  Ana@Example.COM ");

         Assert.Equal("ana@example.com", user.Email);
      }

      [Theory]
      [InlineData("")]
      [InlineData("   ")]
      [InlineData(null)]
      public void Constructor_WithEmptyEmail_Throws(string? email)
      {
         Assert.Throws<BusinessRuleException>(() => CreateUser(email: email!));
      }

      [Fact]
      public void Constructor_WithEmailOf256Chars_Succeeds()   
      {
         var email = new string('a', 244) + "@example.com";   

         var user = CreateUser(email: email);

         Assert.Equal(email, user.Email);
      }

      [Fact]
      public void Constructor_WithEmailOf257Chars_Throws()   
      {
         var email = new string('a', 245) + "@example.com";   

         Assert.Throws<BusinessRuleException>(() => CreateUser(email: email));
      }
   
      [Fact]
      public void DeactivateUser_WhenActive_SetsInactive()
      {
         var user = CreateUser();

         user.DeactivateUser();

         Assert.False(user.IsActive);
      }

      [Fact]
      public void DeactivateUser_WhenAlreadyInactive_DoesNothing()   // BR-U7
      {
         var user = CreateUser();
         user.DeactivateUser();

         user.DeactivateUser();

         Assert.False(user.IsActive);
      }

      [Fact]
      public void ActivateUser_WhenInactive_SetsActive()
      {
         var user = CreateUser();
         user.DeactivateUser();

         user.ActivateUser();

         Assert.True(user.IsActive);
      }

      [Fact]
      public void ActivateUser_WhenAlreadyActive_DoesNothing()   // BR-U7
      {
         var user = CreateUser();

         user.ActivateUser();

         Assert.True(user.IsActive);
      } 

      [Fact]
      public void UpdateUser_UpdatesFieldsAndNormalizesEmail()
      {
         var user = CreateUser();

         user.UpdateUser("  Ana Maria  ", " ANA.MARIA@Example.com ", UserRole.ProjectManager);

         Assert.Equal("Ana Maria", user.Name);
         Assert.Equal("ana.maria@example.com", user.Email);
         Assert.Equal(UserRole.ProjectManager, user.Role);
      }

      [Fact]
      public void UpdateUser_WithEmptyEmail_Throws()
      {
         var user = CreateUser();

         Assert.Throws<BusinessRuleException>(() => user.UpdateUser("Ana", "  ", UserRole.Employee));
      }
   }
}

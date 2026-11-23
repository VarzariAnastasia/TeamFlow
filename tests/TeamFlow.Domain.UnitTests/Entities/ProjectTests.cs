using TeamFlow.Domain.Entities;
using TeamFlow.Domain.Enums;
using TeamFlow.Domain.Exceptions;

namespace TeamFlow.Domain.UnitTests.Entities
{
   public class ProjectTests
   {
      private static readonly DateTime Now = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
      private static readonly DateTime Start = new(2026, 12, 1);

      private static readonly Guid CreatorId = Guid.NewGuid();

      private static Project CreateProject(string name = "Website", string? description = null, DateTime? endDate = null)
          => new(name, description, Start, endDate, CreatorId, Now);

      private static Project CreateArchivedProject()
      {
         var project = CreateProject();
         project.ChangeStatus(ProjectStatus.Completed, Now);
         project.Archive(Now);
         return project;
      }

      [Fact]
      public void Constructor_WithValidData_SetsStatusToPlanned()   // BR-P3
      {
         var project = CreateProject();

         Assert.Equal(ProjectStatus.Planned, project.Status);
      }

      [Fact]
      public void Constructor_WithValidData_SetsCreatorAndCreatedAt()
      {
         var project = CreateProject();

         Assert.NotEqual(Guid.Empty, project.Id);
         Assert.Equal(CreatorId, project.CreatedByUserId);
         Assert.Equal(Now, project.CreatedAt);
         Assert.Null(project.UpdatedAt);
      }

      [Theory]
      [InlineData("")]
      [InlineData("   ")]
      [InlineData(null)]
      public void Constructor_WithEmptyName_Throws(string? name)   // BR-P1
      {
         Assert.Throws<BusinessRuleException>(() => CreateProject(name: name!));
      }

      [Fact]
      public void Constructor_WithNameOf200Chars_Succeeds()   // BR-P1
      {
         var name = new string('a', 200);

         var project = CreateProject(name: name);

         Assert.Equal(name, project.Name);
      }

      [Fact]
      public void Constructor_WithNameOf201Chars_Throws()   // BR-P1
      {
         var name = new string('a', 201);

         Assert.Throws<BusinessRuleException>(() => CreateProject(name: name));
      }

      [Fact]
      public void Constructor_TrimsName()   // BR-P1
      {
         var project = CreateProject(name: "  Website  ");

         Assert.Equal("Website", project.Name);
      }

      [Fact]
      public void Constructor_WithDescriptionOf4000Chars_Succeeds()   
      {
         var description = new string('a', 4000);

         var project = CreateProject(description: description);

         Assert.Equal(description, project.Description);
      }

      [Fact]
      public void Constructor_WithDescriptionOf4001Chars_Throws()  
      {
         var description = new string('a', 4001);

         Assert.Throws<BusinessRuleException>(() => CreateProject(description: description));
      }

      [Theory]
      [InlineData(null)]
      [InlineData("")]
      [InlineData("   ")]
      public void Constructor_WithEmptyDescription_StoresNull(string? description)
      {
         var project = CreateProject(description: description);

         Assert.Null(project.Description);
      }

      [Fact]
      public void Constructor_WithEndDateEarlierThanStartDate_Throws()   // BR-P2
      {
         var endDate = Start.AddDays(-1);

         Assert.Throws<BusinessRuleException>(() => CreateProject(endDate: endDate));
      }

      [Fact]
      public void Constructor_WithEndDateSameAsStartDate_Succeeds()   // BR-P2 boundary
      {
         var project = CreateProject(endDate: Start);

         Assert.Equal(Start, project.EndDate);
      }

      [Fact]
      public void Constructor_WithEndDateAfterStartDate_Succeeds()   // BR-P2
      {
         var endDate = Start.AddDays(30);

         var project = CreateProject(endDate: endDate);

         Assert.Equal(endDate, project.EndDate);
      }

      [Fact]
      public void Constructor_WithoutEndDate_Succeeds()   // BR-P2
      {
         var project = CreateProject(endDate: null);

         Assert.Null(project.EndDate);
      }

      [Fact]
      public void Archive_WhenCompleted_SetsStatusToArchived()   // BR-P6
      {
         var project = CreateProject();
         project.ChangeStatus(ProjectStatus.Completed, Now);
         var later = Now.AddHours(1);

         project.Archive(later);

         Assert.Equal(ProjectStatus.Archived, project.Status);
         Assert.Equal(later, project.UpdatedAt);
      }

      [Theory]
      [InlineData(ProjectStatus.Planned)]
      [InlineData(ProjectStatus.Active)]
      [InlineData(ProjectStatus.OnHold)]
      public void Archive_WhenNotCompleted_Throws(ProjectStatus status)   // BR-P6
      {
         var project = CreateProject();
         project.ChangeStatus(status, Now);

         Assert.Throws<BusinessRuleException>(() => project.Archive(Now));
      }

      [Fact]
      public void Archive_WhenAlreadyArchived_DoesNothing()   // idempotent
      {
         var project = CreateProject();
         project.ChangeStatus(ProjectStatus.Completed, Now);
         project.Archive(Now.AddHours(1));

         project.Archive(Now.AddHours(2));

         Assert.Equal(ProjectStatus.Archived, project.Status);
         Assert.Equal(Now.AddHours(1), project.UpdatedAt);
      }  

      [Theory]
      [InlineData(ProjectStatus.Active)]
      [InlineData(ProjectStatus.OnHold)]
      [InlineData(ProjectStatus.Completed)]
      public void ChangeStatus_ToNonArchivedStatus_UpdatesStatusAndUpdatedAt(ProjectStatus newStatus)
      {
         var project = CreateProject();
         var later = Now.AddHours(1);

         project.ChangeStatus(newStatus, later);

         Assert.Equal(newStatus, project.Status);
         Assert.Equal(later, project.UpdatedAt);
      }

      [Fact]
      public void ChangeStatus_ToArchived_Throws()   // BR-P9
      {
         var project = CreateProject();
         project.ChangeStatus(ProjectStatus.Completed, Now);

         Assert.Throws<BusinessRuleException>(() => project.ChangeStatus(ProjectStatus.Archived, Now));
         Assert.Equal(ProjectStatus.Completed, project.Status);
      }

      [Fact]
      public void ChangeStatus_WhenArchived_Throws()   // BR-P7
      {
         var project = CreateArchivedProject();

         Assert.Throws<BusinessRuleException>(() => project.ChangeStatus(ProjectStatus.Active, Now));
         Assert.Equal(ProjectStatus.Archived, project.Status);
      }

      [Fact]
      public void Update_WhenNotArchived_UpdatesFields()
      {
         var project = CreateProject();
         var later = Now.AddHours(1);
         var newEnd = Start.AddDays(10);

         project.Update("  Website v2  ", "New description", Start, newEnd, later);

         Assert.Equal("Website v2", project.Name);
         Assert.Equal("New description", project.Description);
         Assert.Equal(newEnd, project.EndDate);
         Assert.Equal(later, project.UpdatedAt);
      }

      [Fact]
      public void Update_WithEndDateEarlierThanStartDate_Throws()   // BR-P2
      {
         var project = CreateProject();

         Assert.Throws<BusinessRuleException>(() =>
            project.Update("Website", null, Start, Start.AddDays(-1), Now));
      }

      [Fact]
      public void Update_WithEmptyName_Throws()   // BR-P1
      {
         var project = CreateProject();

         Assert.Throws<BusinessRuleException>(() => project.Update("   ", null, Start, null, Now));
      }

      [Fact]
      public void Update_WhenArchived_Throws()   // BR-P7
      {
         var project = CreateArchivedProject();

         Assert.Throws<BusinessRuleException>(() =>
            project.Update("Website1", project.Description, project.StartDate, project.EndDate, Now));
         Assert.Equal("Website", project.Name);
      }
   }
}

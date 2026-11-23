using TeamFlow.Domain.Entities;
using TeamFlow.Domain.Enums;
using TeamFlow.Domain.Exceptions;

namespace TeamFlow.Domain.UnitTests.Entities
{
   public class TaskItemTests
   {
      private static readonly DateTime Now = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
      private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

      private static readonly Guid ProjectId = Guid.NewGuid();
      private static readonly Guid CreatorId = Guid.NewGuid();

      private static TaskItem CreateTask(string title = "Write tests", string? description = null, DateOnly? dueDate = null)
          => new(ProjectId, title, description, CreatorId, dueDate, Now);

      private static void SetStatus(TaskItem task, TaskItemStatus status, DateTime now)
          => task.ChangeStatus(status, now);

      private static TaskItem CreateArchivedTask()
      {
         var task = CreateTask();
         task.ArchiveTask(Now);
         return task;
      }

      [Fact]
      public void Constructor_WithValidData_SetsDefaults()   // BR-T1, BR-T3
      {
         var task = CreateTask();

         Assert.NotEqual(Guid.Empty, task.Id);
         Assert.Equal(ProjectId, task.ProjectId);
         Assert.Equal(CreatorId, task.CreatedByUserId);
         Assert.Equal(TaskItemStatus.ToDo, task.Status);
         Assert.Equal(TaskPriority.Medium, task.Priority);
         Assert.Null(task.AssignedToUserId);
         Assert.Null(task.CompletedAt);
         Assert.False(task.IsArchived);
         Assert.Equal(Now, task.CreatedAt);
         Assert.Equal(Now, task.UpdatedAt);
      }

      [Theory]
      [InlineData("")]
      [InlineData("   ")]
      [InlineData(null)]
      public void Constructor_WithEmptyTitle_Throws(string? title)   // BR-T2
      {
         Assert.Throws<BusinessRuleException>(() => CreateTask(title: title!));
      }

      [Fact]
      public void Constructor_WithTitleOf200Chars_Succeeds()   // BR-T2
      {
         var title = new string('a', 200);

         var task = CreateTask(title: title);

         Assert.Equal(title, task.Title);
      }

      [Fact]
      public void Constructor_WithTitleOf201Chars_Throws()   // BR-T2
      {
         Assert.Throws<BusinessRuleException>(() => CreateTask(title: new string('a', 201)));
      }

      [Fact]
      public void Constructor_TrimsTitle()   // BR-T2
      {
         var task = CreateTask(title: "  Write tests  ");

         Assert.Equal("Write tests", task.Title);
      }

      [Fact]
      public void Constructor_WithDescriptionOf4001Chars_Throws()   
      {
         Assert.Throws<BusinessRuleException>(() => CreateTask(description: new string('a', 4001)));
      }

      [Theory]
      [InlineData(null)]
      [InlineData("   ")]
      public void Constructor_WithEmptyDescription_StoresNull(string? description)
      {
         var task = CreateTask(description: description);

         Assert.Null(task.Description);
      }

     

      [Fact]
      public void Constructor_WithDueDateYesterday_Throws()   // BR-T5
      {
         Assert.Throws<BusinessRuleException>(() => CreateTask(dueDate: Today.AddDays(-1)));
      }

      [Fact]
      public void Constructor_WithDueDateToday_Succeeds()   // BR-T5 boundary
      {
         var task = CreateTask(dueDate: Today);

         Assert.Equal(Today, task.DueDate);
      }

      [Fact]
      public void Constructor_WithoutDueDate_Succeeds()   // BR-T5
      {
         var task = CreateTask(dueDate: null);

         Assert.Null(task.DueDate);
      }

      [Fact]
      public void Update_WithPastDueDate_IsAllowed()   // BR-T5
      {
         var task = CreateTask(dueDate: Today);
         var muchLater = Now.AddDays(5);

         task.Update(task.Title, null, null, task.Status, task.Priority, Today, muchLater);

         Assert.Equal(Today, task.DueDate);
      }

      

      [Fact]
      public void IsOverdue_WhenDueYesterdayAndOpen_ReturnsTrue()   // BR-T12
      {
         var task = CreateTask(dueDate: Today);
         var tomorrow = Now.AddDays(1);

         Assert.True(task.IsOverdue(tomorrow));
      }

      [Fact]
      public void IsOverdue_WhenDueToday_ReturnsFalse()   // BR-T12
      {
         var task = CreateTask(dueDate: Today);

         Assert.False(task.IsOverdue(Now));
      }

      [Fact]
      public void IsOverdue_WithoutDueDate_ReturnsFalse()   // BR-T12
      {
         var task = CreateTask(dueDate: null);

         Assert.False(task.IsOverdue(Now.AddYears(1)));
      }

      [Fact]
      public void IsOverdue_WhenDone_ReturnsFalse()   // BR-T12
      {
         var task = CreateTask(dueDate: Today);
         SetStatus(task, TaskItemStatus.Done, Now);

         Assert.False(task.IsOverdue(Now.AddDays(1)));
      }

     

      [Fact]
      public void ChangeStatus_ToDone_SetsCompletedAt()   // BR-T6
      {
         var task = CreateTask();
         var later = Now.AddHours(1);

         SetStatus(task, TaskItemStatus.Done, later);

         Assert.Equal(TaskItemStatus.Done, task.Status);
         Assert.Equal(later, task.CompletedAt);
      }

      [Fact]
      public void ChangeStatus_OutOfDone_ClearsCompletedAt()   // BR-T7
      {
         var task = CreateTask();
         SetStatus(task, TaskItemStatus.Done, Now);

         SetStatus(task, TaskItemStatus.InProgress, Now.AddHours(1));

         Assert.Equal(TaskItemStatus.InProgress, task.Status);
         Assert.Null(task.CompletedAt);
      }

      [Fact]
      public void ChangeStatus_DoneToDone_KeepsOriginalCompletedAt()   // BR-T8
      {
         var task = CreateTask();
         SetStatus(task, TaskItemStatus.Done, Now);

         SetStatus(task, TaskItemStatus.Done, Now.AddHours(1));

         Assert.Equal(Now, task.CompletedAt);
      }

      [Fact]
      public void Update_SetsUpdatedAt()   // BR-T10
      {
         var task = CreateTask();
         var later = Now.AddHours(1);

         task.Update("New title", null, null, TaskItemStatus.InProgress, TaskPriority.High, null, later);

         Assert.Equal("New title", task.Title);
         Assert.Equal(TaskPriority.High, task.Priority);
         Assert.Equal(later, task.UpdatedAt);
      }      

      [Fact]
      public void ChangeStatus_SetsStatusAndUpdatedAt()   // BR-T10
      {
         var task = CreateTask();
         var later = Now.AddHours(1);

         task.ChangeStatus(TaskItemStatus.InReview, later);

         Assert.Equal(TaskItemStatus.InReview, task.Status);
         Assert.Equal(later, task.UpdatedAt);
      }

      [Fact]
      public void Update_ToDone_SetsCompletedAt()   // BR-T6: Update reuses ChangeStatus
      {
         var task = CreateTask();
         var later = Now.AddHours(1);

         task.Update(task.Title, null, null, TaskItemStatus.Done, task.Priority, null, later);

         Assert.Equal(TaskItemStatus.Done, task.Status);
         Assert.Equal(later, task.CompletedAt);
      }

      [Fact]
      public void AssignToUser_SetsAssigneeAndUpdatedAt()   // BR-T4 
      {
         var task = CreateTask();
         var userId = Guid.NewGuid();
         var later = Now.AddHours(1);

         task.AssignToUser(userId, later);

         Assert.Equal(userId, task.AssignedToUserId);
         Assert.Equal(later, task.UpdatedAt);
      }

      [Fact]
      public void UnassignUser_ClearsAssignee()   // BR-T4
      {
         var task = CreateTask();
         task.AssignToUser(Guid.NewGuid(), Now);

         task.UnassignUser(Now.AddHours(1));

         Assert.Null(task.AssignedToUserId);
      }

      [Fact]
      public void ArchiveTask_SetsIsArchivedAndUpdatedAt()   // BR-T11
      {
         var task = CreateTask();
         var later = Now.AddHours(1);

         task.ArchiveTask(later);

         Assert.True(task.IsArchived);
         Assert.Equal(later, task.UpdatedAt);
      }

      [Fact]
      public void ArchiveTask_WhenAlreadyArchived_DoesNothing()   // idempotent
      {
         var task = CreateTask();
         task.ArchiveTask(Now.AddHours(1));

         task.ArchiveTask(Now.AddHours(2));

         Assert.True(task.IsArchived);
         Assert.Equal(Now.AddHours(1), task.UpdatedAt);
      }

      [Fact]
      public void ChangeStatus_WhenArchived_Throws()   // BR-T11
      {
         var task = CreateArchivedTask();

         Assert.Throws<BusinessRuleException>(() => task.ChangeStatus(TaskItemStatus.Done, Now));
         Assert.Equal(TaskItemStatus.ToDo, task.Status);
      }

      [Fact]
      public void Update_WhenArchived_Throws()   // BR-T11
      {
         var task = CreateArchivedTask();

         Assert.Throws<BusinessRuleException>(() =>
            task.Update("New title", null, null, TaskItemStatus.ToDo, TaskPriority.High, null, Now));
         Assert.Equal("Write tests", task.Title);
      }

      [Fact]
      public void AssignToUser_WhenArchived_Throws()   // BR-T11
      {
         var task = CreateArchivedTask();

         Assert.Throws<BusinessRuleException>(() => task.AssignToUser(Guid.NewGuid(), Now));
         Assert.Null(task.AssignedToUserId);
      }

      [Fact]
      public void UnassignUser_WhenArchived_Throws()   // BR-T11
      {
         var task = CreateArchivedTask();

         Assert.Throws<BusinessRuleException>(() => task.UnassignUser(Now));
      }
   }
}

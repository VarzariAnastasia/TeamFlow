using TeamFlow.Domain.Entities;

namespace TeamFlow.Domain.UnitTests.Entities
{
   public class ProjectMemberTests
   {
      private static readonly DateTime Now = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

      [Fact]
      public void Constructor_SetsProjectUserAndJoinedAt()
      {
         var projectId = Guid.NewGuid();
         var userId = Guid.NewGuid();

         var member = new ProjectMember(projectId, userId, Now);

         Assert.Equal(projectId, member.ProjectId);
         Assert.Equal(userId, member.UserId);
         Assert.Equal(Now, member.JoinedAt);
      }
   }
}

namespace TeamFlow.Domain.Entities
{
   public class ProjectMember
   {
      public Guid ProjectId { get; private set; }
      public Guid UserId { get; private set; }
      public DateTime JoinedAt { get; private set; }
      public ProjectMember(Guid projectId, Guid userId, DateTime joinedAt)
      {
         ProjectId = projectId;
         UserId = userId;
         JoinedAt = joinedAt;
      }
   }
}

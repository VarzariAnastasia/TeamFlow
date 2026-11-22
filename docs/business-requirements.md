# TeamFlow: Business Requirements

This document defines **what** TeamFlow must do. It is the source of truth for implementation and tests.

- **Business rules** (`BR-…`) are invariants that must always hold. Most are enforced in **Domain**, some in **Application**.
- **User stories** (`US-…`) describe what a user wants to achieve. They map to **Application** use cases, then to **API** endpoints and **React** pages.
- **Acceptance criteria** (`AC-…`) are written as *Given / When / Then* and map one-to-one to automated tests.

Each item has an ID. Use IDs in test names or comments, e.g. `// BR-P2`, so every rule can be traced to a test.

---

## 1. Glossary

| Term | Meaning |
|---|---|
| **Administrator** | Manages users and can see and do everything. |
| **Project Manager (PM)** | Creates projects and manages the projects they are a member of. |
| **Employee** | Works on tasks assigned to them in projects they are a member of. |
| **Member** | A user linked to a project through `ProjectMembers`. The creator of a project is automatically a member. |
| **Active user** | A user with `IsActive = true`. Inactive users cannot sign in or receive new tasks. |
| **Open task** | A task whose status is not `Done`. |
| **Overdue task** | An open task whose `DueDate` is before today (UTC date). |
| **Archived project** | A project with status `Archived`. It is read-only. |

---

## 2. Implementation order

| Step | Layer | What you build | Tests you write |
|---|---|---|---|
| 1 | **Domain** | Enums, entities, business-rule methods (§3) | Unit tests per `BR-…` |
| 2 | **Application** | Services, DTOs, validators, authorization (§4, §5) | Unit tests per `AC-…`, with fakes or EF Core InMemory/SQLite |
| 3 | **API** | Controllers, auth, error handling (§6) | Integration tests via `WebApplicationFactory` |
| 4 | **React** | Pages and components (§7) | Vitest + Testing Library + MSW |

Within each layer, go feature by feature: **Users → Projects → Tasks → Comments → Dashboard**.

---

## 3. Domain: entities and business rules

### 3.1 Enums

| Enum | Values |
|---|---|
| `UserRole` | `Administrator`, `ProjectManager`, `Employee` |
| `ProjectStatus` | `Planned`, `Active`, `OnHold`, `Completed`, `Archived` |
| `TaskItemStatus` | `ToDo`, `InProgress`, `InReview`, `Done` |
| `TaskPriority` | `Low`, `Medium`, `High`, `Critical` |

> Name the task entity `TaskItem` and its status enum `TaskItemStatus`. `Task` and `TaskStatus` clash with `System.Threading.Tasks`.

### 3.2 Entities

| Entity | Fields |
|---|---|
| `User` | Id, FirstName, LastName, Email, Role, IsActive, CreatedAt |
| `Project` | Id, Name, Description, StartDate, EndDate?, Status, CreatedByUserId, CreatedAt, UpdatedAt |
| `ProjectMember` | ProjectId, UserId, JoinedAt |
| `TaskItem` | Id, ProjectId, Title, Description, AssignedToUserId?, CreatedByUserId, Status, Priority, DueDate?, CreatedAt, UpdatedAt, **CompletedAt?**, **IsArchived** |
| `TaskComment` | Id, TaskId, UserId, Content, CreatedAt, UpdatedAt? |

> **Additions to the original spec:** `TaskItem.CompletedAt` (required by "completing a task records its completion date"), `TaskItem.IsArchived` (for "delete or archive tasks"), and `Project.UpdatedAt`.

### 3.3 Field limits

| Field | Rule |
|---|---|
| `User.FirstName`, `User.LastName` | Required, 1–100 characters, trimmed |
| `User.Email` | Required, valid format, max 256, **unique**, stored lowercase |
| `Project.Name` | Required, 1–200 characters, trimmed |
| `Project.Description` | Optional, max 4000 |
| `TaskItem.Title` | Required, 1–200 characters, trimmed |
| `TaskItem.Description` | Optional, max 4000 |
| `TaskComment.Content` | Required, 1–2000 characters, trimmed |

### 3.4 Business rules: Users

| ID | Rule | Enforced in |
|---|---|---|
| **BR-U1** | A new user is active by default, and `CreatedAt` is set at creation. | Domain |
| **BR-U2** | Email is unique across all users, case-insensitive. | Application + DB unique index |
| **BR-U3** | An inactive user cannot sign in. | Application (auth) |
| **BR-U4** | An administrator cannot deactivate their own account. | Application |
| **BR-U5** | The last active administrator cannot be deactivated or have their role changed. | Application |
| **BR-U6** | Deactivating a user does **not** unassign their tasks. Existing tasks stay assigned so history is preserved, but the user cannot receive new assignments (see BR-T4). | Domain |
| **BR-U7** | Deactivating an already inactive user, or activating an active one, does nothing. It is idempotent and raises no error. | Domain |

### 3.5 Business rules: Projects

| ID | Rule | Enforced in |
|---|---|---|
| **BR-P1** | A project name is required (after trimming) and at most 200 characters. | Domain |
| **BR-P2** | If an end date is set, it cannot be earlier than the start date. | Domain |
| **BR-P3** | A new project starts with status `Planned`. | Domain |
| **BR-P4** | The creator of a project is automatically added as a member. | Domain / Application |
| **BR-P5** | Only an **Administrator**, or a **Project Manager who is a member** of the project, can edit it, change its status, or manage its members. | Application |
| **BR-P6** | Only a project with status `Completed` can be archived. | Domain |
| **BR-P7** | An archived project is **read-only**. Its details, members and tasks cannot be changed, and it cannot leave `Archived`. | Domain |
| **BR-P8** | An archived project cannot receive new tasks. | Domain |
| **BR-P9** | Status changes go through dedicated methods. `Archived` is only reachable through `Archive()` (BR-P6), never through a generic "set status". | Domain |
| **BR-P10** | Only **active** users can be added as members. | Domain / Application |
| **BR-P11** | A user cannot be added to the same project twice. | Domain + DB unique index (ProjectId, UserId) |
| **BR-P12** | A member who still has **open** tasks assigned in the project cannot be removed. Reassign or complete those tasks first. | Domain / Application |
| **BR-P13** | The project creator cannot be removed from the project. | Domain |
| **BR-P14** | Completion percentage = `Done tasks / all non-archived tasks × 100`, rounded **down** to a whole number. A project with no tasks has 0%. | Domain (pure function) |

### 3.6 Business rules: Tasks

| ID | Rule | Enforced in |
|---|---|---|
| **BR-T1** | Every task belongs to exactly one project, set at creation and never changed. | Domain |
| **BR-T2** | A task title is required (after trimming) and at most 200 characters. | Domain |
| **BR-T3** | A new task starts with status `ToDo`. Priority defaults to `Medium` if not given. | Domain |
| **BR-T4** | A task can only be assigned to a user who is **active** and a **member of the task's project**. A task may also be unassigned. | Domain |
| **BR-T5** | When creating a task, the due date cannot be in the past. A past due date may remain on existing tasks. | Domain |
| **BR-T6** | Moving a task to `Done` sets `CompletedAt` to the current time. | Domain |
| **BR-T7** | Moving a task out of `Done` clears `CompletedAt`. | Domain |
| **BR-T8** | Setting a task to the status it already has does nothing. In particular, `CompletedAt` is not overwritten. | Domain |
| **BR-T9** | Tasks in an archived project cannot be created, edited, reassigned, have their status changed, be archived, or receive comments. | Domain |
| **BR-T10** | Every change to a task updates `UpdatedAt`. | Domain |
| **BR-T11** | Deleting a task **archives** it (`IsArchived = true`, a soft delete). Archived tasks are hidden from lists and the dashboard and cannot be modified. | Domain |
| **BR-T12** | A task is overdue when it is open and `DueDate < today` (UTC). | Domain (pure function) |

### 3.7 Business rules: Comments

| ID | Rule | Enforced in |
|---|---|---|
| **BR-C1** | Comment content is required (after trimming) and at most 2000 characters. | Domain |
| **BR-C2** | Only the author of a comment can edit it. Editing sets `UpdatedAt`. | Domain / Application |
| **BR-C3** | The author or an Administrator can delete a comment. Deletion is permanent. | Application |
| **BR-C4** | Comments cannot be added to, edited on, or deleted from archived tasks or tasks in archived projects. | Domain |
| **BR-C5** | Comments are always returned in chronological order (oldest first). | Application |

### 3.8 Time

| ID | Rule |
|---|---|
| **BR-X1** | All timestamps are stored in **UTC**. |
| **BR-X2** | Domain methods receive the current time as a parameter (e.g. `task.ChangeStatus(status, now)`), or Application gets it from an injected `TimeProvider`. Domain code never calls `DateTime.UtcNow` directly, which keeps time-based rules testable. |

---

## 4. Authorization matrix

Authorization is always enforced by the backend. The UI only hides controls for convenience.

| Action | Administrator | Project Manager | Employee |
|---|---|---|---|
| Manage users (create, edit, deactivate, change role) | ✅ | ❌ | ❌ |
| View the user list | ✅ | ✅ (to add members) | ❌ |
| Create a project | ✅ | ✅ | ❌ |
| View a project | ✅ all | ✅ if member | ✅ if member |
| Edit, change status, archive a project | ✅ | ✅ if member | ❌ |
| Add or remove project members | ✅ | ✅ if member | ❌ |
| Create, edit, assign, archive tasks | ✅ | ✅ if member | ❌ |
| Change a task's status | ✅ | ✅ if member | ✅ **only tasks assigned to them** |
| View tasks | ✅ all | ✅ in their projects | ✅ in their projects |
| Add a comment | ✅ | ✅ if member | ✅ if member |
| Edit a comment | Own only | Own only | Own only |
| Delete a comment | ✅ any | Own only | Own only |

**Not a member? Respond 404, not 403.** When a user asks for a project or task in a project they don't belong to, return *Not Found*. Returning 403 would confirm that the resource exists. Use 403 when the user can see the resource but cannot perform the action, e.g. an Employee trying to edit a project they are a member of.

---

## 5. Application: user stories and acceptance criteria

### Epic 1: User Management

**US-1.1: Create a user**
*As an Administrator, I want to create user accounts with a role, so that employees can sign in to TeamFlow.*

- **AC-1.1.1** Given valid data, when I create a user, then the user is saved as active, with the given role and `CreatedAt` set.
- **AC-1.1.2** Given an email that already exists (in any letter case), when I create a user, then it fails with a validation error on `Email`.
- **AC-1.1.3** Given a missing first name, last name or email, or an invalid email, then it fails with a validation error per field.
- **AC-1.1.4** Given I am not an Administrator, when I try to create a user, then it is rejected as forbidden.

**US-1.2: Update a user**
*As an Administrator, I want to update a user's name, email and role, so that their data stays correct.*

- **AC-1.2.1** Given valid data, when I update a user, then the changes are saved.
- **AC-1.2.2** Given the user is the last active Administrator, when I change their role, then it is rejected (BR-U5).
- **AC-1.2.3** Given the user does not exist, then it fails with *not found*.

**US-1.3: Deactivate a user**
*As an Administrator, I want to deactivate users who leave the company, so that they can no longer access TeamFlow.*

- **AC-1.3.1** Given an active user, when I deactivate them, then `IsActive` becomes false and they cannot sign in.
- **AC-1.3.2** Given I try to deactivate myself, then it is rejected (BR-U4).
- **AC-1.3.3** Given the user is the last active Administrator, then it is rejected (BR-U5).
- **AC-1.3.4** Given the deactivated user has assigned tasks, then those tasks stay assigned (BR-U6).

**US-1.4: Search users**
*As an Administrator, I want to search and filter users, so that I can find someone quickly.*

- **AC-1.4.1** Given a search term, then users whose first name, last name or email contains it (case-insensitive) are returned.
- **AC-1.4.2** Given `isActive=true`, then only active users are returned.
- **AC-1.4.3** Results are paginated (see §5.6) and sorted by last name, then first name, by default.
- **AC-1.4.4** The response never includes password hashes, security stamps or other identity internals.

---

### Epic 2: Project Management

**US-2.1: Create a project**
*As a Project Manager, I want to create a project, so that I can organize my team's work.*

- **AC-2.1.1** Given a valid name and start date, then the project is created with status `Planned`, `CreatedByUserId` set to me, and I am a member (BR-P3, BR-P4).
- **AC-2.1.2** Given an empty or whitespace name, then it fails with a validation error (BR-P1).
- **AC-2.1.3** Given an end date earlier than the start date, then it fails (BR-P2).
- **AC-2.1.4** Given I am an Employee, then it is rejected as forbidden.
- **AC-2.1.5** Creating the project and adding the creator as a member happen in **one transaction**. Either both are saved or neither is.

**US-2.2: Update a project**
*As a Project Manager, I want to update a project's details and status, so that it reflects reality.*

- **AC-2.2.1** Given I am a PM member or an Administrator, then I can change name, description, dates and status (except to `Archived`).
- **AC-2.2.2** Given I am a PM who is **not** a member, then it fails with *not found* (§4).
- **AC-2.2.3** Given the project is archived, then any update is rejected (BR-P7).

**US-2.3: Archive a project**
*As a Project Manager, I want to archive completed projects, so that active lists stay clean while history is kept.*

- **AC-2.3.1** Given a `Completed` project, when I archive it, then its status becomes `Archived`.
- **AC-2.3.2** Given a project in any other status, then archiving is rejected (BR-P6).
- **AC-2.3.3** Given an archived project, then no task in it can be created or changed (BR-P8, BR-T9).

**US-2.4: Manage project members**
*As a Project Manager, I want to add and remove employees, so that the right people work on the project.*

- **AC-2.4.1** Given an active user who is not a member, when I add them, then they become a member.
- **AC-2.4.2** Given an inactive user, then adding them is rejected (BR-P10).
- **AC-2.4.3** Given a user who is already a member, then adding them again is rejected (BR-P11).
- **AC-2.4.4** Given a member with open tasks in the project, then removing them is rejected with a message telling me to reassign their tasks first (BR-P12).
- **AC-2.4.5** Given the project creator, then removing them is rejected (BR-P13).

**US-2.5: Browse projects**
*As any user, I want to see and search the projects I belong to, so that I can find my work.*

- **AC-2.5.1** An Administrator sees all projects. Everyone else sees only projects they are a member of.
- **AC-2.5.2** I can filter by status and search by name. Archived projects are excluded unless I filter for `Archived` explicitly.
- **AC-2.5.3** Each list item shows name, status, start/end dates, member count and completion percentage (BR-P14).
- **AC-2.5.4** Results are paginated (§5.6).

**US-2.6: View project details**
*As a member, I want to see a project's details, members and progress.*

- **AC-2.6.1** Details include description, dates, status, creator name, members (name and role only) and task counts per status.
- **AC-2.6.2** Given I am not a member and not an Administrator, then it fails with *not found*.

---

### Epic 3: Task Management (core module)

**US-3.1: Create a task**
*As a Project Manager, I want to create tasks in my project, so that work is clearly defined.*

- **AC-3.1.1** Given a valid title in an active project, then the task is created with status `ToDo`, default priority `Medium`, and `CreatedByUserId` set to me (BR-T3).
- **AC-3.1.2** Given an empty title, then it fails with a validation error (BR-T2).
- **AC-3.1.3** Given a due date in the past, then it fails (BR-T5).
- **AC-3.1.4** Given the project is archived, then it is rejected (BR-P8).
- **AC-3.1.5** Given the project does not exist, or I am not a member, then it fails with *not found*.

**US-3.2: Assign a task**
*As a Project Manager, I want to assign a task to an employee, so that ownership is clear.*

- **AC-3.2.1** Given an active project member, then the task is assigned to them.
- **AC-3.2.2** Given an inactive user, then it is rejected (BR-T4).
- **AC-3.2.3** Given a user who is not a member of the project, then it is rejected (BR-T4).
- **AC-3.2.4** Given I set the assignee to empty, then the task becomes unassigned.

**US-3.3: Update a task**
*As a Project Manager, I want to edit a task's title, description, priority, due date and assignee.*

- **AC-3.3.1** Given valid changes, then they are saved and `UpdatedAt` changes (BR-T10).
- **AC-3.3.2** Given I am an Employee, then editing these fields is rejected as forbidden (status only, see US-3.4).
- **AC-3.3.3** Given someone else changed the task after I loaded it, then my update is rejected as a conflict instead of silently overwriting their change *(optimistic concurrency, recommended)*.

**US-3.4: Change task status**
*As an Employee, I want to update the status of my tasks, so that my manager sees my progress.*

- **AC-3.4.1** Given a task assigned to me, then I can change its status to any value.
- **AC-3.4.2** Given a task **not** assigned to me, and I am an Employee, then it is rejected as forbidden.
- **AC-3.4.3** Given I move the task to `Done`, then `CompletedAt` is set (BR-T6).
- **AC-3.4.4** Given I move a `Done` task back to `InProgress`, then `CompletedAt` is cleared (BR-T7).
- **AC-3.4.5** Given the task already has that status, then nothing changes (BR-T8).

**US-3.5: Archive (delete) a task**
*As a Project Manager, I want to remove tasks that are no longer relevant.*

- **AC-3.5.1** Given I archive a task, then it disappears from lists and the dashboard but stays in the database (BR-T11).
- **AC-3.5.2** Given an archived task, then it can no longer be modified or commented on.

**US-3.6: Find tasks**
*As any user, I want to search, filter, sort and page through tasks, so that I can focus on what matters.*

- **AC-3.6.1** Filters, all optional and combinable: `projectId`, `status`, `priority`, `assignedToUserId`, `dueBefore`, `dueAfter`, `isOverdue`, `search` (title contains, case-insensitive).
- **AC-3.6.2** Sortable by `dueDate`, `priority`, `status`, `createdAt`, `updatedAt`, `title`, ascending or descending. Default: `dueDate` ascending, with tasks without a due date last. Unknown sort fields are rejected with a validation error.
- **AC-3.6.3** Only tasks in projects I can see are returned, and archived tasks are never returned.
- **AC-3.6.4** Results are paginated **in the database** (§5.6).
- **AC-3.6.5** Each item shows title, project name, assignee name, status, priority, due date and an overdue flag.

**US-3.7: My tasks**
*As an Employee, I want a list of tasks assigned to me across all projects, so that I know what to work on next.*

- **AC-3.7.1** Shows only my non-archived tasks, by default excluding `Done`, sorted by due date.
- **AC-3.7.2** Supports the same filters, sorting and pagination as US-3.6.

**US-3.8: View task details**

- **AC-3.8.1** Shows all task fields, the project name, assignee and creator names, and the comment thread.
- **AC-3.8.2** Given I am not a member of the task's project, then it fails with *not found*.

---

### Epic 4: Task Comments

**US-4.1: Comment on a task**
*As a project member, I want to comment on a task, so that we can discuss it in one place.*

- **AC-4.1.1** Given non-empty content, then the comment is saved with me as author and `CreatedAt` set.
- **AC-4.1.2** Given empty or whitespace content, or more than 2000 characters, then it fails (BR-C1).
- **AC-4.1.3** Given the task or its project is archived, then it is rejected (BR-C4).

**US-4.2: Read comments**
- **AC-4.2.1** Comments are returned oldest first (BR-C5), each with the author's full name, `CreatedAt`, and `UpdatedAt` if edited.
- **AC-4.2.2** The response indicates whether I can edit or delete each comment, so the UI can show the right buttons.

**US-4.3: Edit or delete my comment**
- **AC-4.3.1** Given my own comment, then I can edit it and `UpdatedAt` is set (BR-C2).
- **AC-4.3.2** Given someone else's comment, then editing is rejected as forbidden.
- **AC-4.3.3** Given my own comment, or any comment if I am an Administrator, then I can delete it (BR-C3).

---

### Epic 5: Dashboard

**US-5.1: Work overview**
*As any user, I want a dashboard summarizing my work, so that I can prioritize my day.*

- **AC-5.1.1** Shows the number of **active** projects (status `Active`) I can see.
- **AC-5.1.2** Shows the total number of open tasks assigned to me.
- **AC-5.1.3** Shows my assigned tasks grouped by status, with a count per status, including statuses with 0.
- **AC-5.1.4** Lists my overdue tasks (BR-T12), most overdue first.
- **AC-5.1.5** Lists the 10 most recently updated tasks in projects I can see.
- **AC-5.1.6** Shows the completion percentage of each non-archived project I can see (BR-P14). *Example: 20 tasks with 15 done = 75%.*
- **AC-5.1.7** All counts and percentages are calculated **in the database**, not by loading all tasks into memory.

---

### Epic 6: Authentication

**US-6.1: Sign in**
*As a user, I want to sign in with my email and password, so that I can access my work.*

- **AC-6.1.1** Given valid credentials for an active user, then I receive an access token and my profile (id, name, email, role).
- **AC-6.1.2** Given a wrong email **or** a wrong password, then I get the **same** generic error, without revealing which one was wrong.
- **AC-6.1.3** Given an inactive user, then sign-in fails with the same generic error (BR-U3).
- **AC-6.1.4** Passwords are stored only as hashes, using ASP.NET Core Identity.

**US-6.2: Current user**
- **AC-6.2.1** Given a valid token, then `GET /api/auth/me` returns my profile.
- **AC-6.2.2** Given no token, or an expired or invalid one, then the request fails as unauthenticated.

---

### 5.6 Pagination (all list endpoints)

- **AC-PG-1** `page` defaults to 1 and must be ≥ 1.
- **AC-PG-2** `pageSize` defaults to 20 and must be between 1 and 100.
- **AC-PG-3** The response is `{ items, totalCount, page, pageSize, totalPages }`, where `totalPages = ceil(totalCount / pageSize)`.
- **AC-PG-4** Requesting a page beyond the last returns an empty `items` list with the correct `totalCount`. It is not an error.
- **AC-PG-5** Paging is applied with `Skip/Take` **in SQL**, after filtering and sorting.

---

## 6. API: acceptance criteria

These criteria apply to every endpoint and are verified by **integration tests**.

### 6.1 Status codes

| Situation | Status | Body |
|---|---|---|
| Read succeeded | 200 OK | DTO or paged result |
| Resource created | 201 Created + `Location` header | Created DTO |
| Update or delete with nothing to return | 204 No Content | None |
| Invalid input (validation) | 400 Bad Request | ProblemDetails with an `errors` per field |
| Not signed in, or invalid token | 401 Unauthorized | None or ProblemDetails |
| Signed in but not allowed | 403 Forbidden | ProblemDetails |
| Resource doesn't exist **or isn't visible to you** | 404 Not Found | ProblemDetails |
| Business rule violated, or concurrency conflict | 409 Conflict | ProblemDetails with a readable `detail` |
| Unexpected error | 500 | ProblemDetails **without** stack trace or exception message |

### 6.2 General criteria

- **AC-API-1** Every endpoint except `POST /api/auth/login` requires authentication.
- **AC-API-2** Every error response uses the same ProblemDetails format (RFC 7807).
- **AC-API-3** Unhandled exceptions are logged with a trace id, and the same trace id is returned in the response.
- **AC-API-4** API contracts use DTOs only. Entities are never serialized directly.
- **AC-API-5** Enums are sent as strings (`"InProgress"`, not `1`).
- **AC-API-6** CORS allows only the configured frontend origin.
- **AC-API-7** Secrets such as the JWT signing key and connection strings are not stored in source control.

### 6.3 Endpoints

| Method | Route | Story |
|---|---|---|
| POST | `/api/auth/login` | US-6.1 |
| GET | `/api/auth/me` | US-6.2 |
| GET | `/api/users` | US-1.4 |
| GET | `/api/users/{id}` | US-1.2 |
| POST | `/api/users` | US-1.1 |
| PUT | `/api/users/{id}` | US-1.2 |
| PATCH | `/api/users/{id}/deactivate` | US-1.3 |
| GET | `/api/projects` | US-2.5 |
| GET | `/api/projects/{id}` | US-2.6 |
| POST | `/api/projects` | US-2.1 |
| PUT | `/api/projects/{id}` | US-2.2 |
| PATCH | `/api/projects/{id}/archive` | US-2.3 |
| POST | `/api/projects/{id}/members` | US-2.4 |
| DELETE | `/api/projects/{id}/members/{userId}` | US-2.4 |
| GET | `/api/tasks` | US-3.6 |
| GET | `/api/tasks/{id}` | US-3.8 |
| POST | `/api/tasks` | US-3.1 |
| PUT | `/api/tasks/{id}` | US-3.2, US-3.3 |
| PATCH | `/api/tasks/{id}/status` | US-3.4 |
| DELETE | `/api/tasks/{id}` | US-3.5 |
| GET | `/api/tasks/{taskId}/comments` | US-4.2 |
| POST | `/api/tasks/{taskId}/comments` | US-4.1 |
| PUT | `/api/comments/{id}` | US-4.3 |
| DELETE | `/api/comments/{id}` | US-4.3 |
| GET | `/api/dashboard/summary` | US-5.1 |
| GET | `/api/dashboard/my-tasks` | US-3.7 |
| GET | `/api/dashboard/overdue-tasks` | US-5.1 |

### 6.4 Example integration tests

- `POST /api/projects` with a valid body returns **201** with a `Location` header.
- `GET /api/tasks/99999` returns **404**.
- `POST /api/tasks` with an empty title returns **400** with an `errors.title` entry.
- Any endpoint without a token returns **401**.
- An Employee calling `POST /api/users` gets **403**.
- A PM who is not a member calling `GET /api/projects/{id}` gets **404**.
- Creating a task in an archived project returns **409**.
- With 45 tasks, `GET /api/tasks?page=3&pageSize=20` returns 5 items and `totalCount = 45`.

---

## 7. React: user stories and acceptance criteria

### 7.1 General UI criteria

- **AC-UI-1** While data loads, a loading indicator is shown.
- **AC-UI-2** When a request fails, a readable error is shown using the API's ProblemDetails `title` / `detail`. Validation errors appear next to the fields they belong to.
- **AC-UI-3** After creating, updating or deleting, the affected lists and details refresh without a full page reload.
- **AC-UI-4** Forms validate required fields and lengths **before** submitting, using the same limits as §3.3. The backend still validates everything.
- **AC-UI-5** Controls a user is not allowed to use are hidden or disabled, following §4. Security does not rely on this.
- **AC-UI-6** List filters, sorting and page number are stored in the URL query string, so a filtered view can be bookmarked or shared and survives a refresh.
- **AC-UI-7** On a 401 response, the user is sent to the login page. After signing in, they return to the page they were on.

### 7.2 Pages

| Page | Route | Who | Key criteria |
|---|---|---|---|
| **Login** | `/login` | Everyone | Email + password fields with required validation; shows the generic error on failure (AC-6.1.2); redirects to the dashboard on success. |
| **Dashboard** | `/` | All | Stat cards (active projects, my open tasks), tasks by status, overdue list highlighted in red, recently updated tasks, and a progress bar per project (US-5.1). |
| **Projects** | `/projects` | All | Paginated table with search and status filter; "New project" button for PM/Admin only (US-2.5, US-2.1). |
| **Project details** | `/projects/:id` | Members, Admin | Info, members (add/remove for PM/Admin), the project's task list with filters, and an "Archive" button shown only when the status is `Completed` (US-2.2–2.4, 2.6). |
| **My tasks** | `/my-tasks` | All | My open tasks across projects, sortable and filterable; status can be changed inline (US-3.7, 3.4). |
| **Task details** | `/tasks/:id` | Members, Admin | All fields; edit form for PM/Admin; status dropdown for the assignee; comment thread with add/edit/delete (US-3.3, 3.4, 3.8, Epic 4). |
| **Users** | `/users` | Admin | Paginated table with search and active filter; create/edit form; deactivate button with a confirmation dialog (Epic 1). |

### 7.3 Reusable components

`DataTable`, `Pagination`, `StatusBadge`, `PriorityBadge`, `FormField` (label + input + error), `ConfirmDialog`, `ErrorMessage`, `LoadingSpinner`, `ProtectedRoute`, `Layout` / navigation.

### 7.4 Example frontend tests

- The task list renders a row per task returned by the API.
- Submitting the task form with an empty title shows "Title is required" and sends no request.
- A loading indicator appears while the request is pending.
- When the API returns 409, its `detail` message is shown.
- After a new task is created, the task list shows it without reloading the page.
- An Employee does not see the "New project" button.

---

## 8. Seed data (development only)

| Email | Role | Notes |
|---|---|---|
| admin@teamflow.local | Administrator | |
| pm@teamflow.local | Project Manager | Member of both projects |
| alice@teamflow.local | Employee | Member of "Website Redesign" |
| bob@teamflow.local | Employee | Member of both projects |
| carol@teamflow.local | Employee | **Inactive**, for testing BR-T4 / BR-U3 |

Projects: **"Website Redesign"** (Active, about 25 tasks in mixed statuses, some overdue) and **"Q3 Reporting"** (Completed, all tasks done, ready to archive).

Store development passwords in the seed configuration, never in production settings.

---

## 9. Out of scope for the first version

Audit logging, notifications (Azure Service Bus), background reminder jobs, file attachments, user self-registration, password reset and multi-tenancy. These are optional extensions after the Definition of Done is met.

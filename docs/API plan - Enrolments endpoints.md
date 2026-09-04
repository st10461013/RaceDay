## Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/categories/{categoryId}/enrolments | Enter (enrol in) a category for an event | Participant | `{ }` (participant taken from auth token) | `201 Created` — new enrolment object with bib number |
| GET | /api/enrolments/me | View the logged-in participant's own enrolments (upcoming and past) | Participant | — | `200 OK` — array of enrolments |
| GET | /api/events/{eventId}/enrolments | View all participants enrolled in an event owned by the logged-in organiser | Organiser | — | `200 OK` — array of enrolments |
| DELETE | /api/enrolments/{enrolmentId} | Cancel the logged-in participant's own enrolment | Participant | — | `204 No Content` |

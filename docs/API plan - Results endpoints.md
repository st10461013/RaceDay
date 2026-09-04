## Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/enrolments/{enrolmentId}/result | Capture a participant's result for an enrolment | Organiser | `{ finishTime, position, status }` | `201 Created` — new result object |
| PUT | /api/results/{resultId} | Update/correct a captured result | Organiser | `{ finishTime, position, status }` | `200 OK` — updated result object |
| GET | /api/events/{eventId}/results | View the full results/leaderboard for an event | Public | — | `200 OK` — array of results |
| GET | /api/results/me | View the logged-in participant's personal performance history across all past events | Participant | — | `200 OK` — array of results with event/category context |

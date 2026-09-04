## Routes

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events/{eventId}/routes | List route information for an event | Public | — | `200 OK` — array of routes |
| POST | /api/events/{eventId}/routes | Add a route to an event | Organiser | `{ routeName, distanceKM, elevationGainM, mapURL }` | `201 Created` — new route object |
| PUT | /api/routes/{routeId} | Update a route | Organiser | `{ routeName, distanceKM, elevationGainM, mapURL }` | `200 OK` — updated route object |
| DELETE | /api/routes/{routeId} | Delete a route | Organiser | — | `204 No Content` |

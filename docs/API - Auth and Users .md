# RaceDay — API Endpoint Plan (Part 1, Section B)

This plan covers the functionality described for the two RaceDay roles: **Event Organisers**
(create/manage events, categories, routes, and participant results) and **Participants**
(browse events, enter events, track personal performance history, view live weather and
route information ahead of race day). It is designed to match the six entities in the ERD
(Users, Events, Categories, Routes, Enrolments, Results) and will be implemented as-is in
Part 2. 

## Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Register a new user as either an Organiser or a Participant | Public | `{ role, firstName, lastName, email, password, phoneNumber }` | `201 Created` — new user object (no password hash) |
| POST | /api/auth/login | Authenticate a user and issue a JWT | Public | `{ email, password }` | `200 OK` — `{ token, user }` |

## Users

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/users/me | Get the logged-in user's own profile | Organiser or Participant | — | `200 OK` — user object |
| PUT | /api/users/me | Update the logged-in user's own profile | Organiser or Participant | `{ firstName, lastName, phoneNumber }` | `200 OK` — updated user object |

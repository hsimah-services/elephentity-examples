# User

V1 has one private operator account, provisioned as admin. The reusable model
retains editor/admin roles; multiple accounts and account lifecycle screens are
outside v1. The account has no email. Its username is the login: 3–32 lowercase
letters, digits, dots, hyphens or underscores, starting with a letter or digit,
unique, set at bootstrap and not editable in the UI. The display name is the public
author name on pages and items and cannot be blank. Settings show the username and display
name, edit only the display name, and change the password. Role/active are internal
and not shown.

There is no public registration or HTTP account-creation endpoint. The explicit
operator bootstrap command refuses to create another account when one exists.
Credentials, password hashes, reset tokens and sessions live in a separate
server-owned authentication store, outside the entity and generated profile form.
The signed-in account changes its password over `POST /auth/password` with the
current password and a new one of at least 12 characters; success revokes its other
sessions, and a wrong current password counts against the login rate limit.
Bootstrap is complete only when credentials have been stored; login requires both
valid credentials and an active account. A partial bootstrap is retried/repaired
through the operator procedure rather than an assumed cross-store atomic write.
The model's existing active default stays true for this single-account deployment.

User has no GraphQL integration. The application supplies a narrow authenticated
profile API and a viewer DTO with explicit allowlists. The write policy checks
caller authorization only. No last-active-admin transaction rule is required in
v1 because role changes/deactivation and additional accounts are outside its UI.
Role and active status are checked server-side on every request; account deletion
is omitted. Future lifecycle support would revoke sessions on deactivation.

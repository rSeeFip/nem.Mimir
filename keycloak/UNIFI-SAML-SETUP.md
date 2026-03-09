# UniFi Console SAML SSO Integration Guide

## Overview

This guide enables Single Sign-On (SSO) for UniFi network consoles through Keycloak SAML identity provider. Users can authenticate using their Keycloak credentials without managing separate UniFi passwords.

## Prerequisites

- Keycloak 23+ running in the Mimir infrastructure (port 8080)
- Keycloak admin access (admin/admin in dev)
- UniFi controller or console (v6.0+)
- Network connectivity between UniFi console and Keycloak
- UniFi console URL known and accessible (e.g., `https://unifi.local:8443`)

## Step 1: Import SAML Client into Keycloak

### Option A: Using Keycloak Admin Console (GUI)

1. **Access Keycloak Admin Console:**
   ```
   http://localhost:8080/admin/
   ```
   - Username: `admin`
   - Password: `admin` (or as configured)

2. **Select Mimir Realm:**
   - Click realm selector (top-left dropdown)
   - Select `mimir`

3. **Import Client:**
   - Left sidebar → **Clients**
   - Click **Create** button
   - Toggle **"Import client configuration"**
   - Upload or paste content from `unifi-saml-client.json`
   - Click **Save**

4. **Verify Client Created:**
   - Client ID should be `unifi-console`
   - Protocol should be `SAML`
   - Status should be **Enabled**

### Option B: Using Docker Volume (Recommended for CI/CD)

The client configuration can be automatically imported by adding to `docker-compose.yml`:

```yaml
mimir-keycloak:
  volumes:
    - ./keycloak/realm-export.json:/opt/keycloak/data/import/realm-export.json:ro
    # Keycloak auto-imports *.json from import/ directory with --import-realm flag
```

Currently, Keycloak uses `realm-export.json` for realm configuration. To add the SAML client to automatic import, merge `unifi-saml-client.json` into the `clients` array of `realm-export.json`:

```json
{
  "realm": "mimir",
  "clients": [
    // ... existing clients ...
    // Add unifi-saml-client.json content here
  ]
}
```

## Step 2: Configure UniFi Console for SAML

### Access UniFi Admin Portal

1. Navigate to UniFi console:
   ```
   https://unifi.local:8443/
   ```

2. Login with local admin account (temporary)

3. Navigate to **Settings** → **System Settings** → **Advanced**

### Enable SAML Authentication

1. **SAML Settings Section:**
   - Enable: `SAML Support` (toggle ON)
   
2. **Identity Provider (IdP) Configuration:**
   - **IdP URL:** `http://mimir-keycloak:8080/realms/mimir`
   - **IdP Entity ID:** `http://mimir-keycloak:8080/realms/mimir`
   - **Binding:** `HTTP POST`
   - **X.509 Certificate:** Export from Keycloak (see Step 3)

3. **Service Provider (SP) Configuration:**
   - **SP Entity ID:** `unifi-console`
   - **Assertion Consumer Service URL:** `https://unifi.local:8443/api/login/ubnt_cloud`
   - **Name ID Format:** `Email Address (urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress)`

4. **Attribute Mapping:**
   - **Email Attribute:** `email`
   - **First Name Attribute:** `givenName`
   - **Last Name Attribute:** `surname`
   - **Groups Attribute:** `groups`

5. Save settings and restart UniFi controller

### Step 3: Export Keycloak Certificate

1. In Keycloak Admin Console:
   - Select **mimir** realm
   - Left sidebar → **Realm Settings** → **Keys**
   - Find the active key with algorithm `RSA-SHA-256`
   - Click **Certificate** to view X.509 certificate
   - Copy the certificate content (including `-----BEGIN CERTIFICATE-----` and `-----END CERTIFICATE-----`)

2. Paste certificate into UniFi SAML settings under **X.509 Certificate**

## Step 4: Create Users in Keycloak

Users authenticating via SAML must exist in Keycloak mimir realm with email addresses:

1. Keycloak Admin Console → **Users**
2. Click **Create new user**
3. Fill in:
   - **Username:** (unique identifier)
   - **Email:** (required for SAML email attribute)
   - **First Name:** (maps to `givenName`)
   - **Last Name:** (maps to `surname`)
   - **Email Verified:** Toggle ON
   - **Enabled:** Toggle ON

4. Set password:
   - **Credentials** tab
   - **Set Password**
   - Enter password
   - Toggle **Temporary** OFF
   - Click **Set Password**

5. Assign realm roles (optional):
   - **Role Mapping** tab
   - Assign roles like `admin`, `user`, or `readonly`
   - Roles appear in SAML `roles` attribute

## Step 5: Configure UniFi User Provisioning

### Link Keycloak Users to UniFi

After SAML is enabled, users can authenticate but may need role mapping:

1. **First Login:**
   - User clicks "Single Sign-On" or SAML button on UniFi login
   - Redirected to Keycloak login
   - After authentication, user is created in UniFi with:
     - Email from SAML assertion
     - Name from SAML `givenName` + `surname` attributes

2. **Assign UniFi Roles:**
   - UniFi Admin → **Users**
   - Find newly created user
   - Assign role: `Admin`, `Operator`, `View Only`, etc.
   - This step may require manual intervention until provisioning is automated

## SAML Attribute Mapping Reference

| UniFi Attribute | SAML Attribute Name | Keycloak Source | Format |
|------------------|---------------------|-----------------|--------|
| Email | `email` | User email | String |
| First Name | `givenName` | User firstName | String |
| Last Name | `surname` | User lastName | String |
| Groups | `groups` | User groups | String (multi-valued) |
| Roles | `roles` | User realm roles | String (multi-valued) |

### How Attribute Mappers Work

The `unifi-saml-client.json` includes protocol mappers that transform Keycloak user attributes into SAML assertion attributes:

- **email mapper:** User's email field → SAML `email` attribute
- **firstName mapper:** User's firstName field → SAML `givenName` attribute
- **lastName mapper:** User's lastName field → SAML `surname` attribute
- **groups mapper:** User group membership → SAML `groups` attribute
- **role mapper:** Realm roles → SAML `roles` attribute

These attributes are included in the SAML assertion sent to UniFi after successful authentication.

## Troubleshooting

### Issue: "SAML authentication failed" or blank error

**Diagnosis:**
- Check Keycloak is running: `docker ps | grep keycloak`
- Verify IdP URL is accessible from UniFi console network

**Solution:**
```bash
# From UniFi container/host
curl -I http://mimir-keycloak:8080/realms/mimir
# Should return 200 OK
```

### Issue: Certificate validation fails

**Diagnosis:**
- X.509 certificate in UniFi settings is invalid or expired

**Solution:**
1. Verify certificate copied correctly (including line breaks)
2. Check certificate expiry:
   ```bash
   echo "-----BEGIN CERTIFICATE-----
   <cert content>
   -----END CERTIFICATE-----" | openssl x509 -noout -dates
   ```
3. If expired, regenerate Keycloak signing key and re-export certificate

### Issue: User email not populated after SAML login

**Diagnosis:**
- Keycloak user missing email address
- Email mapper not configured correctly

**Solution:**
1. Verify Keycloak user has email: Keycloak Admin → Users → Select user → **Email** field filled
2. Check mapper config in `unifi-saml-client.json`: `email` mapper should reference `user.attribute: "email"`
3. Regenerate SAML metadata in UniFi settings

### Issue: SAML metadata retrieval fails

**Diagnosis:**
- UniFi cannot reach Keycloak metadata endpoint
- Keycloak realm name incorrect

**Solution:**
- Verify metadata URL format: `http://mimir-keycloak:8080/realms/mimir`
- Check firewall/network ACLs between UniFi and Keycloak
- Verify DNS resolution of `mimir-keycloak` from UniFi network

## Integration Points

### Environment Variables for Production

Update `docker-compose.override.yml` or `.env`:

```bash
# UniFi Console URL (for SAML ACS and SLO endpoints)
UNIFI_CONSOLE_URL=https://unifi.prod.example.com:8443

# Keycloak realm and client
KEYCLOAK_REALM=mimir
KEYCLOAK_CLIENT_ID=unifi-console

# Keycloak admin for certificate retrieval
KEYCLOAK_ADMIN_URL=http://keycloak.prod.example.com:8080
KEYCLOAK_ADMIN_USER=admin
KEYCLOAK_ADMIN_PASSWORD=${KEYCLOAK_ADMIN_PASSWORD}
```

Then substitute in `unifi-saml-client.json`:

```bash
envsubst < keycloak/unifi-saml-client.json > keycloak/unifi-saml-client.prod.json
```

## Testing SAML Authentication

### Manual Test Flow

1. **Clear browser cookies** for UniFi domain
2. **Navigate to UniFi login:** `https://unifi.local:8443/`
3. **Click "Single Sign-On"** or SAML button (if visible)
4. **Redirected to Keycloak login**
5. **Enter Keycloak credentials** (e.g., admin@mimir.local / admin)
6. **Approve SAML consent** (if prompted)
7. **Redirected back to UniFi dashboard** logged in as the Keycloak user
8. **Verify user profile:** Top-right → Account → Email should match Keycloak email

### Debug SAML Assertions

Enable SAML debugging in UniFi:

1. SSH to UniFi host or container
2. Edit `/usr/lib/unifi/data/system.properties`:
   ```properties
   # Enable SAML debug logging
   log.console.level=DEBUG
   log.saml.level=DEBUG
   ```
3. Restart UniFi: `systemctl restart unifi` or container restart
4. Tail logs: `tail -f /usr/lib/unifi/logs/system.log | grep -i saml`
5. Attempt SAML login and inspect log output

## Security Considerations

1. **Always use HTTPS in production** for SAML endpoints
2. **Validate certificates** in production Keycloak setups
3. **Use strong admin passwords** in Keycloak (not default `admin/admin`)
4. **Rotate signing keys** periodically (Keycloak → Realm Settings → Keys)
5. **Limit SAML assertion lifetime** (default 3600s in config)
6. **Enable SAML assertion encryption** in high-security environments
7. **Monitor SAML authentication logs** for failed login attempts
8. **Use Network ACLs** to restrict Keycloak access to authorized networks

## References

- [Keycloak SAML Documentation](https://www.keycloak.org/docs/latest/server_admin/#saml)
- [UniFi SAML Configuration](https://help.ui.com/articles/SAML)
- [OASIS SAML 2.0 Standard](http://docs.oasis-open.org/security/saml/v2.0/)

## Support & Next Steps

- **For integration issues:** Check Keycloak logs: `docker logs mimir-keycloak`
- **For UniFi issues:** Check UniFi system logs as described in Troubleshooting
- **For automated provisioning:** See T20 (SAML Integration Tests) for verification suite
- **For automated user provisioning:** Future enhancement using SCIM (Guardrail G4: do not implement)

---

**Last Updated:** 2026-03-08  
**Configuration Version:** 1.0  
**Status:** Ready for deployment

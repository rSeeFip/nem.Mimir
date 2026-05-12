# Deployment Runbook - nem.Mimir

## Deployment Steps
1. **Pull Image**:
   ```bash
   docker pull ghcr.io/nem/nem.mimir:latest
   ```
2. **Update Service**:
   ```bash
   docker-compose up -d nem.mimir
   ```

## Rollback
1. **Docker**:
   ```bash
   docker-compose up -d nem.mimir:<previous-tag>
   ```
2. **ArgoCD**:
   ```bash
   argocd app rollback nem.mimir --revision <previous-revision>
   ```

## Health Check
- **Endpoint**: `http://localhost:5030/health`
- **Verification**: Ensure the response is `Healthy`.

## Troubleshooting
- **Logs**: `docker logs nem.mimir --tail 100`
- **Restart**: `docker-compose restart nem.mimir`

## Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Development|Staging|Production
- `ConnectionStrings__DefaultConnection`: PostgreSQL connection string
- `RabbitMQ__Host`: RabbitMQ host
- `Jwt__Authority`: nem.Sentinel URL
- `Jwt__Audience`: nem.Mimir

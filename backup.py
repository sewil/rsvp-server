import os
from datetime import datetime, timezone

filename = f"rsvp_{datetime.now(timezone.utc).strftime('%Y%m%d%H%M')}.sql"

os.system(f"mysqldump -u rsvp-backup -pblah --databases rsvp > {filename}")

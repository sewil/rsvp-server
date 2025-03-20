#pip install google-api-python-client
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload
from google.oauth2 import service_account
import os
from datetime import datetime, timezone
import re

now = datetime.now(timezone.utc)

filename = f"rsvp_{now.strftime('%Y%m%d%H%M')}.sql"
print(f"Backing up to {filename}...")
os.system(f"mysqldump -u rsvp -pmypassword --databases rsvp > {filename}")

cwd = os.getcwd()
for f in os.listdir("."):
    path = os.path.join(cwd, f)
    if os.path.isfile(path) and re.match("rsvp_\d{12}\.sql", f) and os.stat(path).st_ctime < now.timestamp() - 3 * 86400:
        print(f"Deleting old backup {f}...")
        # Delete backup files older than 3 days
        os.remove(path)

exc_dir = os.path.dirname(os.path.realpath(__file__))
SCOPES = ['https://www.googleapis.com/auth/drive']
SERVICE_ACCOUNT_FILE = os.path.join(exc_dir, 'DataSvr', 'rsvp-454314-250657a03be1.json')
PARENT_FOLDER_ID = "1wx0OApuyBWAsz8sUyHxiq-sPI7V9XBBU"

def google_auth():
    creds = service_account.Credentials.from_service_account_file(SERVICE_ACCOUNT_FILE, scopes=SCOPES)
    return creds

def gdrive_upload(file_path):
    creds = google_auth()
    service = build('drive', 'v3', credentials=creds)
    (_, file_name) = os.path.split(file_path)
    print(f"Uploading {filename} to Google Drive...")

    file_metadata = {
        'name': file_name,
        'parents': [PARENT_FOLDER_ID]
    }

    media = MediaFileUpload(file_path, resumable=True)
    service.files().create(
        body=file_metadata,
        media_body=media,
    ).execute()
    print("Uploaded!")

if now.hour == 9 and now.minute == 0:
    file_path = os.path.realpath(filename)
    gdrive_upload(file_path)
else:
    print(f"Time is {now.strftime('%H:%M')}, skipping Google Drive upload...")

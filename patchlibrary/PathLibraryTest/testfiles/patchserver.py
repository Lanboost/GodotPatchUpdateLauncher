from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse
import os

class MyHandler(BaseHTTPRequestHandler):

    def custom_send(self, resp):
        # send 200 response
        self.send_response(200)
        # send response headers
        self.end_headers()
        # send the body of the response
        self.wfile.write(bytes(resp, "utf-8"))

    def custom_send_file(self, file):
        # send 200 response
        self.send_response(200)
        file_size = os.path.getsize(file)
        self.send_header('Content-length', str(file_size))
        # send response headers
        self.end_headers()
        # send the body of the response
        with open(file, "rb") as f:
            while 1:
                byte_s = f.read(10)
                if not byte_s:
                    break
                
                self.wfile.write(byte_s)

    def do_GET(self):
        # first we need to parse it
        parsed = urlparse(self.path)
        # get the query string
        query_string = parsed.query
        # get the request path, this new path does not have the query string
        path = parsed.path[1:]
        if path:
            paths = path.split("/")
            if paths[0] == "latest_version":
                self.custom_send("2")
            elif paths[0] == "patches":
                # should be from 0 to 2 but who knows.. :)
                self.custom_send("patch1.patch\npatch2.patch")
            elif paths[0] == "patch":
                patch = paths[1]
                self.custom_send_file(patch)

httpd = HTTPServer(('localhost', 10000), MyHandler)
httpd.serve_forever()
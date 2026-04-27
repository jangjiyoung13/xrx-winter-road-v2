const express = require('express');
const QRCode = require('qrcode');
const app = express();

const PORT = 3000;

app.use(express.json());

// 메인 페이지
app.get('/', (req, res) => {
    res.send(`
        <!DOCTYPE html>
        <html>
        <head>
            <title>QR Code Test</title>
            <style>
                body { font-family: Arial, sans-serif; text-align: center; padding: 50px; }
                .button { background: #007bff; color: white; padding: 10px 20px; border: none; border-radius: 5px; cursor: pointer; }
                .qr-code { margin: 20px; }
            </style>
        </head>
        <body>
            <h1>QR Code Test</h1>
            <button class="button" onclick="generateQR()">Generate QR Code</button>
            <div id="qrCode" class="qr-code"></div>
            
            <script>
                async function generateQR() {
                    try {
                        const response = await fetch('/api/generate-qr', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ text: 'http://121.135.96.124:3000/test' })
                        });
                        
                        const data = await response.json();
                        if (data.success) {
                            document.getElementById('qrCode').innerHTML = '<img src="' + data.qrCode + '" alt="QR Code">';
                        } else {
                            alert('QR generation failed: ' + data.error);
                        }
                    } catch (error) {
                        alert('Error: ' + error.message);
                    }
                }
            </script>
        </body>
        </html>
    `);
});

// QR 코드 생성 API
app.post('/api/generate-qr', async (req, res) => {
    try {
        console.log('QR generation request received:', req.body);
        const { text } = req.body;
        
        if (!text) {
            return res.status(400).json({
                success: false,
                error: 'Text is required'
            });
        }
        
        console.log('Generating QR code for:', text);
        const qrCode = await QRCode.toDataURL(text);
        console.log('QR code generated successfully');
        
        res.json({ success: true, qrCode });
    } catch (error) {
        console.error('QR generation error:', error);
        res.status(500).json({
            success: false,
            error: error.message
        });
    }
});

app.listen(PORT, '0.0.0.0', () => {
    console.log('Test server running on http://121.135.96.124:' + PORT);
});

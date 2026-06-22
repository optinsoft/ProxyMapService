const http = require('http');

http.createServer((req, res) => {
  res.writeHead(200, {
    'Content-Type': 'application/javascript',
    'Transfer-Encoding': 'chunked' // Форсируем chunked
  });

  // Отправляем JS-код по кусочкам с паузой
  res.write("console.log('Chunk 1 loaded');\n");
  setTimeout(() => {
    res.write("console.log('Chunk 2 loaded');\n");
    res.end(); // Финальный нулевой чанк
  }, 500);

}).listen(3000, () => console.log('Тестовый сервер: http://localhost:3000'));
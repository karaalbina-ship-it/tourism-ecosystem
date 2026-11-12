$utf8 = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = $utf8
Set-Location (Split-Path $PSScriptRoot -Parent)

docker compose cp neo4j/demo.cypher neo4j:/tmp/demo.cypher 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Не вдалося скопіювати demo.cypher у контейнер' }
$output = docker compose exec -T neo4j cypher-shell -u neo4j -p Tourism_Graph1 --format plain -f /tmp/demo.cypher
if ($LASTEXITCODE -ne 0) { throw 'Помилка виконання demo.cypher' }
[IO.File]::WriteAllLines("$PSScriptRoot\demo-output.txt", $output, $utf8)
$output
